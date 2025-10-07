using System.Net;
using CommentService.Interface;

namespace CommentService.Clients
{
    internal sealed class ArticleExistenceHttpClient(HttpClient http, ILogger<ArticleExistenceHttpClient> logger) : IArticleExistenceClient
    {
        

        private readonly HttpClient _http = http;
        private readonly ILogger<ArticleExistenceHttpClient> _logger = logger;

        public async Task<bool> Exists(int articleId, CancellationToken ct)
        {
            

            var resp = await _http.GetAsync($"Article/{articleId}?searchAll=true", ct);
            if (resp.StatusCode == HttpStatusCode.OK) return true;
            if (resp.StatusCode == HttpStatusCode.NotFound) return false;

            _logger.LogInformation("Unexpected response validating article {ArticleId}: {StatusCode}.", articleId, resp.StatusCode);
            return false;
        }
    }
}
