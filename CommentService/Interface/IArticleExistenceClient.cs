namespace CommentService.Interface
{
    public interface IArticleExistenceClient
    {
        Task<bool> Exists(int articleId, CancellationToken ct);
    }
}
