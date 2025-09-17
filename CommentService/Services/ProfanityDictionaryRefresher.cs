using CommentService.Interface;

namespace CommentService.Services
{
    public class ProfanityDictionaryRefresher(IProfanityClient client, ILocalProfanityFilter local, ILogger<ProfanityDictionaryRefresher> logger) : BackgroundService
    {
        private readonly IProfanityClient _client = client;
        private readonly ILocalProfanityFilter _local = local;
        private readonly ILogger<ProfanityDictionaryRefresher> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = TimeSpan.FromMinutes(10);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var words = await _client.GetDictionaryAsync(stoppingToken);
                    if (words.Length > 0)
                    {
                        _local.ReplaceDictionary(words);
                        _logger.LogInformation("Profanity dictionary updated with {Count} words.", words.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed refreshing profanity dictionary. ");
                }

                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
