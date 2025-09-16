namespace CommentService.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
