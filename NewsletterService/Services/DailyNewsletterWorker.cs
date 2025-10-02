namespace NewsletterService.Services
{
    internal sealed class DailyNewsletterWorker(IHttpClientFactory clientFactory, ILogger<DailyNewsletterWorker> logger, IConfiguration config) : BackgroundService
    {
        private readonly IHttpClientFactory _clientFactory = clientFactory;
        private readonly ILogger<DailyNewsletterWorker> _logger = logger;
        private readonly IConfiguration _config = config;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = GetInterval();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnce(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Daily newsletter failed");
                }

                await Task.Delay(interval, stoppingToken);
                interval = GetInterval();
            }
        }

        private TimeSpan GetInterval() => TimeSpan.TryParse(_config["Newsletter:DailyInterval"], out var ts) && ts > TimeSpan.Zero
            ? ts
            : TimeSpan.FromHours(24);

        private async Task RunOnce(CancellationToken ct)
        {
            var client = _clientFactory.CreateClient("ArticleService");
            var url = "Article?page=1&pageSize=50&includeGlobal=true";

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ArticleService query failed");
                return;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            // Add parse and assemble email. Placeholder log for the moment
            _logger.LogInformation("Daily digest acquired {Length} chars (EMAIL IMPLEMENTATION MISSING)", body.Length);
        }
    }
}
