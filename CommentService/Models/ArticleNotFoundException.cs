using System.Runtime.Serialization;

namespace CommentService.Models
{
    [Serializable]
    public sealed class ArticleNotFoundException : Exception
    {
        public int ArticleId { get; }

        public ArticleNotFoundException(int articleId) : base($"Article {articleId} was not found.")
        {
            ArticleId = articleId;
        }

        private ArticleNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context){}

    }
}
