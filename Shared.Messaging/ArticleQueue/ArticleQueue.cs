using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging.ArticleQueue.Interface;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Shared.Messaging.ArticleQueue.Model;

namespace Shared.Messaging.ArticleQueue
{
    internal sealed class RabbitMqArticleQueue(IConfiguration config, ILogger<RabbitMqArticleQueue> logger)
        : IArticleQueue, IDisposable
    {
        private readonly ILogger<RabbitMqArticleQueue> _logger = logger;

        private readonly string _host = config["RabbitMQ:Host"] ?? "rabbitmq";
        private readonly int _port = int.TryParse(config["RabbitMQ:Port"], out var p) ? p : 5672;
        private readonly string _user = config["RabbitMQ:User"] ?? "guest";
        private readonly string _pass = config["RabbitMQ:Password"] ?? "guest";
        private readonly string _exchange = config["RabbitMQ:Exchange"] ?? "article.published";

        private readonly ushort _consumerDispatchConcurrency =
            ushort.TryParse(config["RabbitMQ:ConsumerDispatchConcurrency"], out var c) ? c : (ushort)0;

        private readonly ushort _prefetch =
            ushort.TryParse(config["RabbitMQ:PrefetchCount"], out var pf) ? pf : (ushort)10;

        private ConnectionFactory? _factory;
        private IConnection? _connection;
        private IChannel? _pubChannel;

        // To limit number of threads used 
        private readonly SemaphoreSlim _connLock = new(1, 1);
        private readonly SemaphoreSlim _pubLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // I don't know about this
        private static readonly TextMapPropagator Propgator = new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator()
        });

        // Parameterless for testing to keep fail-open behavior
        public RabbitMqArticleQueue() : this(new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new LoggerFactory().CreateLogger<RabbitMqArticleQueue>())
        {

        }

        private async Task<IConnection> EnsureConnection(CancellationToken ct = default)
        {
            if (_connection is { IsOpen: true }) return _connection;

            await _connLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _factory ??= new ConnectionFactory
                {
                    HostName = _host,
                    Port = _port,
                    UserName = _user,
                    Password = _pass,
                    ConsumerDispatchConcurrency = _consumerDispatchConcurrency
                };
                _connection = await _factory.CreateConnectionAsync("article-queue", cancellationToken: ct)
                    .ConfigureAwait(false);
                return _connection;
            }
            // Release the thread
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


                var conn = await EnsureConnection(ct).ConfigureAwait(false);
                var ch = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

                // To recreate if broker closes the channel, maybe remove?
                ch.ChannelShutdownAsync += (_, __) =>
                {
                    _pubChannel = null;
                    return Task.CompletedTask;
                };

                await ch.ExchangeDeclareAsync(exchange: _exchange, type: ExchangeType.Fanout, durable: true,
                    autoDelete: false, cancellationToken: ct).ConfigureAwait(false);

                _pubChannel = ch;
                return ch;
            }

            // Release the thread
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
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.Second)
                };

                var ctx = new PropagationContext(Activity.Current?.Context ?? default, Baggage.Current);
                Propgator.Inject(ctx, props, static (p, key, value) =>
                {
                    p.Headers ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    p.Headers[key] = Encoding.UTF8.GetBytes(value);
                });

                await ch.BasicPublishAsync(exchange: _exchange, routingKey: "", mandatory: false,
                    basicProperties: props, body: body, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Publish failed. Message may be lost");
            }
        }

        public async Task SubscribeAsync(string subscriberName,
            Func<PublishedArticle, PropagationContext, CancellationToken, Task> handler, CancellationToken ct)
        {
            try
            {
                var conn = await EnsureConnection(ct).ConfigureAwait(false);
                var ch = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

                await ch.ExchangeDeclareAsync(
                    exchange: _exchange,
                    type: ExchangeType.Fanout,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: ct).ConfigureAwait(false);

                var qok = await ch.QueueDeclareAsync(
                    queue: "",
                    durable: false,
                    exclusive: true,
                    autoDelete: true,
                    arguments: null,
                    cancellationToken: ct).ConfigureAwait(false);

                var queueName = qok.QueueName;

                await ch.QueueBindAsync(queue: queueName, exchange: _exchange, routingKey: "", cancellationToken: ct)
                    .ConfigureAwait(false);

                await ch.BasicQosAsync(prefetchSize: 0, prefetchCount: _prefetch, global: false, cancellationToken: ct)
                    .ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(ch);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var headers = ea.BasicProperties?.Headers ?? new Dictionary<string, object?>();
                    var parent = Propgator.Extract(default, headers, static (h, key) =>
                    {
                        if (h.TryGetValue(key, out var val))
                        {
                            return val switch
                            {
                                byte[] b => new[] { Encoding.UTF8.GetString(b) },
                                ReadOnlyMemory<byte> m => new[] { Encoding.UTF8.GetString(m.Span) },
                                string s => new[] { s },
                                _ => Array.Empty<string>()
                            };
                        }

                        return Array.Empty<string>();
                    });

                    try
                    {
                        var msg = JsonSerializer.Deserialize<PublishedArticle>(ea.Body.Span, JsonOptions) ??
                                  throw new InvalidOperationException("Invalid ArticleQueue.");
                        await handler(msg, parent, ct).ConfigureAwait(false);
                        await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Subscriber {Subscriber} failed.", subscriberName);
                        await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct)
                            .ConfigureAwait(false);
                    }
                };
                await ch.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: ct)
                    .ConfigureAwait(false);

                // Keep subscription alive
                while (!ct.IsCancellationRequested) await Task.Delay(1000, ct).ConfigureAwait(false);

                await ch.CloseAsync(cancellationToken: ct).ConfigureAwait(false);
                ch.Dispose();
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Subscribe failed for {Subscriber}. Continuing (failopen)", subscriberName);
            }
        }

        public void Dispose()
        {
            try
            {
                _pubChannel?.Dispose();
            }
            catch
            {
            }

            try
            {
                _connection?.Dispose();
            }
            catch
            {
            }

            _connLock.Dispose();
            _pubLock.Dispose();
        }
    }
}
