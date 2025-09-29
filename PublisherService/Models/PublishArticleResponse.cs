namespace PublisherService.Models
{
    public sealed class PublishArticleResponse
    {
        public Guid CorrelationId { get; init; }
        public string Status { get; init; } = "Queued";
    }
}
