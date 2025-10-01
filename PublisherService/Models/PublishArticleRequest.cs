using System.ComponentModel.DataAnnotations;

namespace PublisherService.Models
{
    public sealed class PublishArticleRequest
    {
        [Required, MaxLength(180)]
        public required string Title { get; init; }
        [Required]
        public required string Content { get; init; }
        //public string? Author { get; init; }
        public string? Continent { get; init; }
    }
}
