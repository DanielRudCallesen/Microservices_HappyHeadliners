using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Messaging.ArticleQueue.Model
{
    // For article publish event I need to change the model a bit
    // CorrelationId is required, used for idempotency and tracing continuity
    public sealed class PublishedArticle
    {
        public required  Guid CorrelationId{ get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public string? Author { get; init; } // Maybe remove? Got no Authentication yet
        public string? Continent { get; init; } // string to avoid tight coupling with enum
        public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
