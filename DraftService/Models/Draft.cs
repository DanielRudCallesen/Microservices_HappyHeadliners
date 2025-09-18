using System.ComponentModel.DataAnnotations;

namespace DraftService.Models
{
    public class Draft
    {
        [Key] public int Id { get; set; }
        public int ArticleId { get; set; }
        [Required, MaxLength(180)] public string Title { get; set; } = string.Empty;
        [Required] public string Content { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? Continent { get; set; }
        public string ContentHash { get; set; }
    }
}
