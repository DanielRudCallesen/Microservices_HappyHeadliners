using System.Net;
using CommentService.Interface;

namespace CommentService.Clients
{
    internal sealed class ArticleExistenceHttpClient(HttpClient http, ILogger<ArticleExistenceHttpClient> logger) : IArticleExistenceClient
    {
        private static readonly string[] Continents =
            ["Africa", "Antarctica", "Asia", "Europe", "NorthAmerica", "Australia", "SouthAmerica"];

        private readonly HttpClient _http = http;
        private readonly ILogger<ArticleExistenceHttpClient> _logger = logger;

        public async Task<bool> Exists(int articleId, string? continent, CancellationToken ct)
        {
            // Tries the provided continent.
            if (!string.IsNullOrWhiteSpace(continent))
            {
                var uri = $"Article/{articleId}?continent={Uri.EscapeDataString(continent)}&includeGlobalFallback=true";
                var resp = await _http.GetAsync(uri, ct);
                return resp.StatusCode == HttpStatusCode.OK;
            }
            // Try Global
            {
                var resp = await _http.GetAsync($"Article/{articleId}", ct);
                if (resp.StatusCode == HttpStatusCode.OK) return true;
            }
            // Try other continents until one is found.
            foreach (var c in Continents)
            {
                var uri = $"Article/{articleId}?continent={Uri.EscapeDataString(c)}&includeGlobalFallback=true";
                var resp = await _http.GetAsync(uri, ct);
                if (resp.StatusCode == HttpStatusCode.OK) return true;
            }

            _logger.LogInformation("Article {ArticleId} not found across any shard.", articleId);
            return false;
        }
    }
}
