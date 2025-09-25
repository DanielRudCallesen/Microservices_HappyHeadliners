// Build config for local RabbitMQ

using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using Shared.Messaging;
using Shared.Messaging.ArticleQueue.Interface;
using Shared.Messaging.ArticleQueue.Model;




var mqHost = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
    ? "host.docker.internal"
    : "127.0.0.1";

var cfg = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["RabbitMQ:Host"] = mqHost,
        ["RabbitMQ:Port"] = "5672",
        ["RabbitMQ:User"] = "guest",
        ["RabbitMQ:Password"] = "guest",
        ["RabbitMQ:Exchange"] = "article.published",
        ["RabbitMQ:PrefetchCount"] = "10",
        ["RabbitMQ:QueueName"] = "mqsmoke.test"
    })
    .Build();

// Setup DI
var services = new ServiceCollection()
    .AddLogging(b => b.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss "))
    .AddSingleton<IConfiguration>(cfg)
    // Register the queue implementation
    .AddArticleQueue(cfg) // requires ServiceCollectionExtensions in Shared.Messaging
    .BuildServiceProvider();

var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("MqSmoke");
var queue = services.GetRequiredService<IArticleQueue>();

using var tcp = new TcpClient();
try
{
    await tcp.ConnectAsync(mqHost, 5672);
    logger.LogInformation("TCP OK: Connected to RabbitMQ at localhost:5672");
}
catch (Exception ex)
{
    logger.LogError(ex, "TCP FAIL: Cannot connect to localhost:5672. Is the container running and port mapped?");
}

// Separate tokens: one for subscriber lifetime, one for publishing
using var subCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));  // keep consumer alive


var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

// 1) Subscribe (runs until canceled)
_ = Task.Run(async () =>
{
    await queue.SubscribeAsync("smoke-subscriber", async (msg, parent, ct) =>
    {
        logger.LogInformation("Received: {Msg}", JsonSerializer.Serialize(msg));
        received.TrySetResult(true);
        await Task.CompletedTask;
    }, subCts.Token);
});

// 2) Publish a test message
await Task.Delay(1500); // small delay to let subscription start
using (var pubCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    await queue.PublishAsync(new PublishedArticle
    {
        Id = 42,
        Title = "Test Article",
        Content = "This is a test article.",
        Author = "TestAuthor",
        Continent = "Global",
        PublishedAt = DateTimeOffset.UtcNow
    }, pubCts.Token);
}

// 3) Wait for receipt or timeout
if (await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(8))) == received.Task)
{
    logger.LogInformation("OK: message round-trip verified.");
}
else
{
    logger.LogWarning("Timeout: message was not received.");
}


subCts.Cancel();