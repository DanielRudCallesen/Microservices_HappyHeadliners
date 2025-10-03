using System.Net;
using CommentService.Interface;

namespace CommentService.Clients
{
    internal sealed class ArticleExistenceHttpClient(HttpClient http, ILogger<ArticleExistenceHttpClient> logger) : IArticleExistenceClient
    {
        

        private readonly HttpClient _http = http;
        private readonly ILogger<ArticleExistenceHttpClient> _logger = logger;

        public async Task<bool> Exists(int articleId, string? continent, CancellationToken ct)
        {
            var uri = continent is null
                ? $"Article/{articleId}"
                : $"Article/{articleId}?continent={Uri.EscapeDataString(continent)}&includeGlobalFallBack=true";

            var resp = await _http.GetAsync(uri, ct);
            if (resp.StatusCode == HttpStatusCode.OK) return true;
            if (resp.StatusCode == HttpStatusCode.NotFound) return false;

            _logger.LogInformation("Unexpected response validating article {ArticleId}: {StatusCode}.", articleId, resp.StatusCode);
            return false;
        }
    }
}
