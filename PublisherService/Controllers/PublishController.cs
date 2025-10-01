using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PublisherService.Models;
using Shared.Messaging.ArticleQueue.Interface;
using Shared.Messaging.ArticleQueue.Model;

namespace PublisherService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PublishController : ControllerBase
    {
        private readonly IArticleQueue _queue;
        private readonly ILogger<PublishController> _logger;
        private static readonly ActivitySource ActivitySource = new("PublisherService");

        public PublishController(IArticleQueue queue, ILogger<PublishController> logger)        {
            _queue = queue;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<PublishArticleResponse>> Publish(PublishArticleRequest request,
            CancellationToken ct)
        {
            using var activtiy = ActivitySource.StartActivity("PublishArticle", ActivityKind.Producer);

            var correlationId = Guid.NewGuid();
            activtiy?.SetTag("article.correlation_id", correlationId);
            activtiy?.SetTag("article.title", request.Title);
            var continent = string.IsNullOrWhiteSpace(request.Continent) ? "Global" : request.Continent.Trim();

            var evt = new PublishedArticle
            {
                CorrelationId = correlationId,
                Title = request.Title,
                Content = request.Content,
                //Author = request.Author,
                Continent = continent,
                PublishedAt = DateTimeOffset.UtcNow
            };

            await _queue.PublishAsync(evt, ct);
            _logger.LogInformation("Queued article CorrelationId={CorrelationId} Title={Title}", correlationId, request.Title);

            return Accepted(new PublishArticleResponse { CorrelationId = correlationId });
        }
    }
}
