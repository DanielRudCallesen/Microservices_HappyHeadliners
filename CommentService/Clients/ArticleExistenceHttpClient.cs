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
                ? $"Article/{articleId}/exists"
                : $"Article{articleId}/exists?continent={Uri.EscapeDataString(continent)}&includeGlobalFallBack=true";

            var head = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri), ct);
            if (head.StatusCode == HttpStatusCode.OK) return true;
            if (head.StatusCode == HttpStatusCode.NotFound) return false;

            var get = await _http.GetAsync(uri, ct);
            if (get.StatusCode == HttpStatusCode.OK) return true;
            if (get.StatusCode == HttpStatusCode.NotFound) return false;

            _logger.LogInformation("Unexpected response validating article {ArticleId}: {StatusCode}.", articleId, get.StatusCode);
            return false;
        }
    }
}
