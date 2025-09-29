using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging.ArticleQueue.Interface;
using Shared.Messaging.ArticleQueue.Model;

namespace Shared.Messaging.ArticleQueue
{
    internal sealed class RabbitMqArticleQueue(IConfiguration config, ILogger<RabbitMqArticleQueue> logger)
        : IArticleQueue, IDisposable
    {
        private readonly ILogger<RabbitMqArticleQueue> _logger = logger;
        private readonly IConfiguration _config = config;

        private readonly string _host = config["RabbitMQ:Host"] ?? "rabbitmq";
        private readonly int _port = int.TryParse(config["RabbitMQ:Port"], out var p) ? p : 5672;
        private readonly string _user = config["RabbitMQ:User"] ?? "guest";
        private readonly string _pass = config["RabbitMQ:Password"] ?? "guest";
        private readonly string _exchange = config["RabbitMQ:Exchange"] ?? "article.published";
        private readonly string? _queueName = config["RabbitMQ:QueueName"];
        private readonly bool _ensureQueueOnPublish =
            bool.TryParse(config["RabbitMQ:EnsureQueueOnPublish"], out var eq) && eq;

        private readonly ushort _consumerDispatchConcurrency =
            ushort.TryParse(config["RabbitMQ:ConsumerDispatchConcurrency"], out var c) ? c : (ushort)1;

        private readonly ushort _prefetch =
            ushort.TryParse(config["RabbitMQ:PrefetchCount"], out var pf) ? pf : (ushort)10;

        private readonly bool _namedQueueDurable =
            bool.TryParse(config["RabbitMQ:QueueDurable"], out var qd) ? qd : true;

        private ConnectionFactory? _factory;
        private IConnection? _connection;
        private IChannel? _pubChannel;

        private readonly SemaphoreSlim _connLock = new(1, 1);
        private readonly SemaphoreSlim _pubLock = new(1, 1);
        private readonly SemaphoreSlim _pubSendLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly TextMapPropagator Propgator =
            new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator()
            });

        public RabbitMqArticleQueue() : this(new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new LoggerFactory().CreateLogger<RabbitMqArticleQueue>())
        { }

        private async Task<IConnection> EnsureConnection(CancellationToken ct = default)
        {
            if (_connection is { IsOpen: true }) return _connection;

            await _connLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_connection is { IsOpen: true }) return _connection;

                _factory ??= new ConnectionFactory
                {
                    HostName = _host,
                    Port = _port,
                    UserName = _user,
                    Password = _pass,
                    VirtualHost = "/",
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                    ConsumerDispatchConcurrency = _consumerDispatchConcurrency
                };
                _connection = await _factory.CreateConnectionAsync("article-queue", cancellationToken: ct)
                    .ConfigureAwait(false);
                _logger.LogInformation("RabbitMQ connection opened to {Host}:{Port} as {User}", _host, _port, _user);
                return _connection;
            }
            finally
            {
                _connLock.Release();
            }
        }

        private async Task<IChannel> EnsurePublisherChannelAsync(CancellationToken ct = default)
        {
            if (_pubChannel is not null) return _pubChannel;

            await _pubLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_pubChannel is not null) return _pubChannel;

                var conn = await EnsureConnection(ct).ConfigureAwait(false);
                var ch = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

                ch.ChannelShutdownAsync += (_, _) =>
                {
                    _logger.LogWarning("Publisher channel closed by broker; will recreate on next publish.");
                    _pubChannel = null;
                    return Task.CompletedTask;
                };

                ch.BasicReturnAsync += (_, args) =>
                {
                    _logger.LogError("Unroutable message returned. Exchange={Exchange} RoutingKey={Key} ReplyCode={Code} ReplyText={Text}",
                        args.Exchange, args.RoutingKey, args.ReplyCode, args.ReplyText);
                    return Task.CompletedTask;
                };

                await ch.ExchangeDeclareAsync(exchange: _exchange, type: ExchangeType.Fanout, durable: true,
                    autoDelete: false, cancellationToken: ct).ConfigureAwait(false);

                // (Optional) proactively ensure queue & binding so publishes succeed even if subscriber not yet running.
                if (_ensureQueueOnPublish && !string.IsNullOrWhiteSpace(_queueName))
                {
                    await ch.QueueDeclareAsync(queue: _queueName,
                        durable: _namedQueueDurable,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: ct).ConfigureAwait(false);

                    await ch.QueueBindAsync(queue: _queueName, exchange: _exchange, routingKey: "",
                        cancellationToken: ct).ConfigureAwait(false);

                    _logger.LogInformation("Ensured queue {Queue} bound to {Exchange} during publisher initialization.",
                        _queueName, _exchange);
                }

                _pubChannel = ch;
                _logger.LogInformation("Publisher channel created on exchange {Exchange}", _exchange);
                return ch;
            }
            finally
            {
                _pubLock.Release();
            }
        }

        public async Task PublishAsync(PublishedArticle message, CancellationToken ct)
        {
            try
            {
                var ch = await EnsurePublisherChannelAsync(ct).ConfigureAwait(false);
                var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
                var props = new BasicProperties
                {
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = new Dictionary<string, object?>(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                var ctx = new PropagationContext(Activity.Current?.Context ?? default, Baggage.Current);
                Propgator.Inject(ctx, props, static (p, key, value) =>
                {
                    p.Headers ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    p.Headers[key] = Encoding.UTF8.GetBytes(value);
                });

                await _pubSendLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await ch.BasicPublishAsync(exchange: _exchange, routingKey: "",
                        mandatory: true, basicProperties: props, body: body, cancellationToken: ct)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _pubSendLock.Release();
                }

                _logger.LogInformation("Published {Bytes} bytes to exchange {Exchange} CorrelationId={CorrelationId}",
                    body.Length, _exchange, message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Publish failed. Host={Host} Port={Port} Exchange={Exchange}",
                    _host, _port, _exchange);
            }
        }

        public async Task SubscribeAsync(string subscriberName,
            Func<PublishedArticle, PropagationContext, CancellationToken, Task> handler, CancellationToken ct)
        {
            var attempt = 0;
            while (!ct.IsCancellationRequested)
            {
                attempt++;
                IChannel? ch = null;
                try
                {
                    var conn = await EnsureConnection(ct).ConfigureAwait(false);
                    ch = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

                    await ch.ExchangeDeclareAsync(exchange: _exchange, type: ExchangeType.Fanout,
                        durable: true, autoDelete: false, cancellationToken: ct).ConfigureAwait(false);

                    string queueName;
                    if (!string.IsNullOrWhiteSpace(_queueName))
                    {
                        var qokNamed = await ch.QueueDeclareAsync(queue: _queueName,
                            durable: _namedQueueDurable, exclusive: false, autoDelete: false,
                            arguments: null, cancellationToken: ct).ConfigureAwait(false);
                        queueName = qokNamed.QueueName;
                    }
                    else
                    {
                        var qok = await ch.QueueDeclareAsync(queue: "", durable: false, exclusive: true,
                            autoDelete: true, arguments: null, cancellationToken: ct).ConfigureAwait(false);
                        queueName = qok.QueueName;
                    }

                    await ch.QueueBindAsync(queue: queueName, exchange: _exchange, routingKey: "",
                        cancellationToken: ct).ConfigureAwait(false);

                    await ch.BasicQosAsync(0, _prefetch, false, cancellationToken: ct).ConfigureAwait(false);

                    var consumer = new AsyncEventingBasicConsumer(ch);
                    consumer.ReceivedAsync += async (_, ea) =>
                    {
                        var headers = ea.BasicProperties?.Headers ?? new Dictionary<string, object?>();
                        var parent = Propgator.Extract(default, headers, static (h, key) =>
                        {
                            if (!h.TryGetValue(key, out var val)) return Array.Empty<string>();
                            return val switch
                            {
                                byte[] b => new[] { Encoding.UTF8.GetString(b) },
                                ReadOnlyMemory<byte> m => new[] { Encoding.UTF8.GetString(m.Span) },
                                string s => new[] { s },
                                _ => Array.Empty<string>()
                            };
                        });

                        try
                        {
                            var msg = JsonSerializer.Deserialize<PublishedArticle>(ea.Body.Span, JsonOptions)
                                      ?? throw new InvalidOperationException("Invalid payload.");
                            await handler(msg, parent, ct).ConfigureAwait(false);
                            await ch.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Handler failed. Subscriber={Subscriber} DeliveryTag={Tag}",
                                subscriberName, ea.DeliveryTag);
                            await ch.BasicNackAsync(ea.DeliveryTag, false, requeue: true, cancellationToken: ct)
                                .ConfigureAwait(false);
                        }
                    };

                    var consumerTag = await ch.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer,
                        cancellationToken: ct).ConfigureAwait(false);

                    _logger.LogInformation(
                        "Subscriber {Subscriber} consuming. Exchange={Exchange} Queue={Queue} ConsumerTag={Tag} Prefetch={Prefetch}",
                        subscriberName, _exchange, queueName, consumerTag, _prefetch);

                    // Keep alive until cancellation.
                    while (!ct.IsCancellationRequested)
                        await Task.Delay(1000, ct).ConfigureAwait(false);

                    break; // Cancellation requested
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Subscribe attempt {Attempt} failed for {Subscriber}. Host={Host} Port={Port} Exchange={Exchange} Queue={Queue}. Retrying...",
                        attempt, subscriberName, _host, _port, _exchange, _queueName ?? "(ephemeral)");

                    try { ch?.Dispose(); } catch { }

                    // Exponential backoff capped at 30s
                    var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(6, attempt))));
                    try { await Task.Delay(delay, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        public void Dispose()
        {
            try { _pubChannel?.Dispose(); } catch { }
            try { _connection?.Dispose(); } catch { }

            _connLock.Dispose();
            _pubLock.Dispose();
            _pubSendLock.Dispose();
        }
    }
}