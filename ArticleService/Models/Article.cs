using System.ComponentModel.DataAnnotations;

namespace ArticleService.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(180)]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Content { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;

        public Continent? Continent { get; set; }
    }
}
