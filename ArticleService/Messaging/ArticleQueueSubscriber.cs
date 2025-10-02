using System.Diagnostics;
using ArticleService.Interfaces;
using ArticleService.Models;
using OpenTelemetry.Context.Propagation;
using Shared.Messaging.ArticleQueue.Interface;
using Shared.Messaging.ArticleQueue.Model;

namespace ArticleService.Messaging
{
    public sealed class ArticleQueueSubscriber : BackgroundService
    {
        private readonly IArticleQueue _queue;
        private readonly IServiceScopeFactory _scopeFactorty;
        private readonly ILogger<ArticleQueueSubscriber> _logger;
        private static readonly ActivitySource ActivitySource = new ("ArticleService");

        public ArticleQueueSubscriber(IArticleQueue queue, IServiceScopeFactory scopeFactory,
            ILogger<ArticleQueueSubscriber> logger)
        {
            _queue = queue;
            _scopeFactorty = scopeFactory;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _queue.SubscribeAsync("article-service", HandleAsync, stoppingToken);
        }

        private async Task HandleAsync(PublishedArticle evt, PropagationContext parent, CancellationToken ct)
        {
            using var activity = ActivitySource.StartActivity("Consume PublishedArticle", ActivityKind.Consumer, parent.ActivityContext);

            activity?.SetTag("message.system", "rabbitmq");
            activity?.SetTag("message.destination", "article.published");
            activity?.SetTag("article.correlation_id", evt.CorrelationId);
            activity?.SetTag("article.title", evt.Title);

            Continent? continent = null;
            if (!string.IsNullOrWhiteSpace(evt.Continent) &&
                !evt.Continent.Equals("Global", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<Continent>(evt.Continent, true, out var parsed))
            {
                continent = parsed;
            }

            try
            {
                using var scope = _scopeFactorty.CreateScope();
                var articleService = scope.ServiceProvider.GetRequiredService<IArticleService>();

                var created =
                    await articleService.PersistFromEventAsync(evt.CorrelationId, evt.Title, evt.Content, continent, evt.PublishedAt.UtcDateTime, ct);
                activity?.SetTag("article.db_id", created.id);
                _logger.LogInformation("Processed article event CorrelationId={CorrelationId} => ArticleId={ArticleId}", evt.CorrelationId, created.id);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Error processing article event CorrelationId={CorrelationId}", evt.CorrelationId);
                throw;
            }
        }
    }
}
