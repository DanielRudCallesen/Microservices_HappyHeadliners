namespace NewsletterService.Model
{
    public sealed class ImmediateArticle
    {
        public required Guid CorrelationId { get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public string? Continent { get; init; }
        public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
