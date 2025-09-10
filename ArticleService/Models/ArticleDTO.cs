using System.ComponentModel.DataAnnotations;

namespace ArticleService.Models
{
    public record ArticleCreateDTO([Required, MaxLength(180)] string Title, [Required] string Content, Continent? Continent);
    public record ArticleUpdateDTO([Required, MaxLength(180)] string Title, [Required] string Content, Continent? Continent);

    public record ArticleReadDTO(int id, string Title, string Content, DateTime PublishedDate, Continent? Continent);

}
