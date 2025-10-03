using System.Diagnostics.Metrics;
using System.Text.Json;
using ArticleService.Models;
using StackExchange.Redis;

namespace ArticleService.Caching
{
    public sealed class RedisArticleCache : IArticleCache
    {

        private readonly IRedisConnectionProvider _connection;
        private readonly ILogger<RedisArticleCache> _logger;
        private readonly Meter _meter;
        private readonly Counter<long> _hit;
        private readonly Counter<long> _miss;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
        private readonly TimeSpan _ttl = TimeSpan.FromDays(15);

        public RedisArticleCache(IRedisConnectionProvider connection, ILogger<RedisArticleCache> logger,
            IMeterFactory meterFactory)
        {
            _connection = connection;
            _logger = logger;
            _meter = meterFactory.Create("HappyHeadlines.Cache");
            _hit = _meter.CreateCounter<long>("cache_article_hit");
            _miss = _meter.CreateCounter<long>("cache_article_miss");
        }

        private static string ShardName(Continent? c) => c is null ? "global" : c.Value.ToString().ToLowerInvariant();

        private static string Key(Continent? c, int id) => $"article:{ShardName(c)}:{id}";
        private static string RecentKey(Continent? c) => $"article:{ShardName(c)}:recent";

        public async Task<(ArticleReadDTO? value, bool hit)> TryGet(int id, Continent? continent,
            CancellationToken ct)
        {
            var db = await _connection.GetDatabaseAsync();
            var val = await db.StringGetAsync(Key(continent, id));
            if (val.HasValue)
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<ArticleReadDTO>(val!, JsonOpts);
                    if (dto is not null)
                    {
                        _hit.Add(1);
                        return (dto, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize cached article id={id} shard={Shard}", id, ShardName(continent));
                }
                
            }
            _miss.Add(1);
            return (null, false);
        }

        public async Task<IReadOnlyList<ArticleReadDTO>> GetRecent(Continent? continent, int skip, int take,
            CancellationToken ct)
        {
            var db = await _connection.GetDatabaseAsync();
            var ids = await db.SortedSetRangeByRankAsync(RecentKey(continent), skip, skip + take - 1, Order.Descending);
            if (ids.Length == 0) return Array.Empty<ArticleReadDTO>();

            var tasks = ids.Select(v => db.StringGetAsync(Key(continent, (int)v))).ToArray();
            await Task.WhenAll(tasks);

            var list = new List<ArticleReadDTO>(tasks.Length);
            foreach (var rv in tasks)
            {
                if(!rv.Result.HasValue) continue;
                try
                {
                    if(JsonSerializer.Deserialize<ArticleReadDTO>(rv.Result!, JsonOpts) is {} dto) list.Add(dto);
                }
                catch{}
            }

            return list;
        }

        public async Task Upsert(ArticleReadDTO dto, Continent? continent, CancellationToken ct)
        {
            if (dto.PublishedDate < DateTime.UtcNow.AddDays(-14)) return;

            var db = await _connection.GetDatabaseAsync();
            var payload = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOpts);
            var batch = db.CreateBatch();
            _ = batch.StringSetAsync(Key(continent, dto.id), payload, _ttl);
            _ = batch.SortedSetAddAsync(RecentKey(continent), dto.id,
                new DateTimeOffset(dto.PublishedDate).ToUnixTimeSeconds());
            batch.Execute();
        }

        public async Task Invalidate(int id, Continent? continent, CancellationToken ct)
        {
            var db = await _connection.GetDatabaseAsync();
            await db.KeyDeleteAsync(Key(continent, id));
        }


        public async Task BulkReplace(IEnumerable<ArticleReadDTO> items, Continent? continent, CancellationToken ct)
        {
            var cutoffScore = new DateTimeOffset(DateTime.UtcNow.AddDays(-14)).ToUnixTimeSeconds();
            var db = await _connection.GetDatabaseAsync();
            var batch = db.CreateBatch();

            foreach (var a in items)
            {
                if (a.PublishedDate < DateTime.UtcNow.AddDays(-14)) continue;
                var payload = JsonSerializer.SerializeToUtf8Bytes(a, JsonOpts);
                _ = batch.StringSetAsync(Key(continent, a.id), payload, _ttl);
                _ = batch.SortedSetAddAsync(RecentKey(continent), a.id, new DateTimeOffset(a.PublishedDate).ToUnixTimeSeconds());
            }
            _ = batch.SortedSetRemoveRangeByScoreAsync(RecentKey(continent), double.NegativeInfinity, cutoffScore - 1);
            batch.Execute();
        }
    }
}
