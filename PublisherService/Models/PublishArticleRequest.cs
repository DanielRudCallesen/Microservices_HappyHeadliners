namespace PublisherService.Models
{
    public sealed class PublishArticleRequest
    {
        public required string Title { get; init; }
        public required string Content { get; init; }
        public string? Author { get; init; }
        public string? Continent { get; init; }
    }
}
