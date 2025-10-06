using System.Diagnostics;
using System.Diagnostics.Metrics;
using ArticleService.Data;
using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Caching
{
    public class ArticleShardCachePrewarmHostedService : BackgroundService
    {

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ArticleShardCachePrewarmHostedService> _logger;
        private readonly TimeSpan _interval;
        private readonly Meter _meter;
        private readonly Counter<long> _runs;
        private readonly Counter<long> _errors;
        private readonly Histogram<double> _duration;

        public ArticleShardCachePrewarmHostedService(IServiceScopeFactory scopeFactory, IConfiguration cfg, ILogger<ArticleShardCachePrewarmHostedService> logger, IMeterFactory meterFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            var minutes = Math.Max(1, cfg.GetValue("ArticleCache:PrewarmIntervalMinutes", 5));
            _interval = TimeSpan.FromMinutes(minutes);
            _meter = meterFactory.Create("HappyHeadlines.Cache");
            _runs = _meter.CreateCounter<long>("cache_article_prewarm_runs");
            _errors = _meter.CreateCounter<long>("cache_article_prewarm_errors");
            _duration = _meter.CreateHistogram<double>("cache_article_prewarm_duration_ms");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunAllShardsOnce(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunAllShardsOnce(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<IArticleRepositoryFactory>();
                var cache = scope.ServiceProvider.GetRequiredService<IArticleCache>();
                var impl = cache as RedisArticleCache;

                await PrewarmShard(scope, factory.CreateGlobal(), null, impl, ct);
                foreach (var c in Enum.GetValues<Continent>())
                    await PrewarmShard(scope, factory.CreateForContinent(c), c, impl, ct);

                sw.Stop();
                _runs.Add(1);
                _duration.Record(sw.Elapsed.TotalMilliseconds);
                _logger.LogInformation("Article cache prewarm completed for all shards in {Ms} ms",
                    sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _errors.Add(1);
                _duration.Record(sw.Elapsed.TotalMilliseconds);
                _logger.LogWarning(ex, "Article cache prewarm failed after {Ms} ms", sw.Elapsed.TotalMilliseconds);
            }
        }

        private static async Task PrewarmShard(IServiceScope scope, IArticleRepository rep, Continent? shard,
            RedisArticleCache? cache, CancellationToken ct)
        {
            if (cache is null) return;

            var repoP = rep as ArticleRepository;
            var ctxField = typeof(ArticleRepository).GetField("_context",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (repoP is null || ctxField?.GetValue(repoP) is not ArticleDbContext dbCtx) return;

            var cutoff = DateTime.UtcNow.AddDays(-14);
            var recent = await dbCtx.Articles.AsNoTracking().Where(a => a.PublishedDate >= cutoff)
                .OrderByDescending(a => a.PublishedDate).Take(4000).ToListAsync(ct);

            var dtos = recent.Select(a => new ArticleReadDTO(a.Id, a.Title, a.Content, a.PublishedDate, a.Continent))
                .ToList();
            await cache.BulkReplace(dtos, shard, ct);
        }
    }
}
