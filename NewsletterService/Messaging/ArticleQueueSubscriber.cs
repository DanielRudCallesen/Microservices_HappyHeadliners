using System.Diagnostics;
using NewsletterService.Model;
using NewsletterService.Services;
using OpenTelemetry.Context.Propagation;
using Shared.Messaging.ArticleQueue.Interface;
using Shared.Messaging.ArticleQueue.Model;

namespace NewsletterService.Messaging
{
    public class ArticleQueueSubscriber : BackgroundService
    {
        private readonly IArticleQueue _queue;
        private readonly IImmediateArticleStore _store;
        private readonly ILogger<ArticleQueueSubscriber> _logger;
        private static readonly ActivitySource ActivitySource = new("NewsletterService.Messaging");

        public ArticleQueueSubscriber(IArticleQueue queue, IImmediateArticleStore store,
            ILogger<ArticleQueueSubscriber> logger)
        {
            _queue = queue;
            _store = store;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _queue.SubscribeAsync("newsletter-service", HandleAsync, stoppingToken);
        }

        private Task HandleAsync(PublishedArticle evt, PropagationContext parent, CancellationToken ct)
        {
            using var activity = ActivitySource.StartActivity("Consume PublishedArticle", ActivityKind.Consumer, parent.ActivityContext);
            var immediate = new ImmediateArticle
            {
                CorrelationId = evt.CorrelationId,
                Title = evt.Title,
                Content = evt.Content,
                Continent = evt.Continent,
                ReceivedAt = DateTimeOffset.UtcNow
            };
            _store.Add(immediate);
            _logger.LogInformation("Captured immediate article CorrelationId={CorrelationId} Title={Title}", evt.CorrelationId, evt.Title);
            return Task.CompletedTask;
        }
    }
}
