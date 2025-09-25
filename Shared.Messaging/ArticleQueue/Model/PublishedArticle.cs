using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Messaging.ArticleQueue.Model
{
    public sealed class PublishedArticle
    {
        public required int Id { get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public string? Author { get; init; } // Maybe remove? Got no Authentication yet
        public string? Continent { get; init; } // string to avoid tight coupling with enum
        public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
