namespace CommentService.Interface
{
    public interface IArticleExistenceClient
    {
        Task<bool> Exists(int articleId, string? continent, CancellationToken ct);
    }
}
