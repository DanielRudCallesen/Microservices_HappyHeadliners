using CommentService.Interface;

namespace CommentService.Services
{
    public class ProfanityHttpClient(HttpClient client, ILogger<ProfanityHttpClient> logger) : IProfanityClient
    {
        private readonly HttpClient _client = client;
        private readonly ILogger<ProfanityHttpClient> _logger = logger;

        private record FilterRequest(string Content);

        private record FilterResponse(string SanitizedContent, bool HasProfanity, string[] Matches);

        public async Task<(string SanitizedContent, bool HasProfanity)> FilterAsync(string content,
            CancellationToken ct)
        {
            var resp = await _client.PostAsJsonAsync("profanity/filter", new FilterRequest(content), ct);
            resp.EnsureSuccessStatusCode();
            var payload = await resp.Content.ReadFromJsonAsync<FilterResponse>(cancellationToken: ct) ??
                          throw new InvalidOperationException("Invalid response from ProfanityService. ");
            return (payload.SanitizedContent, payload.HasProfanity);
        }

        public async Task<string[]> GetDictionaryAsync(CancellationToken ct)
        {
            try
            {
                return await _client.GetFromJsonAsync<string[]>("Profanity/words", cancellationToken: ct) ??
                       Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
