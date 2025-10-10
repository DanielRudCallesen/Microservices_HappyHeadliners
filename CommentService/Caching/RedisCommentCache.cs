using System.Diagnostics.Metrics;
using System.Text.Json;
using CommentService.Models;
using StackExchange.Redis;

namespace CommentService.Caching
{
    public sealed class RedisCommentCache : ICommentCache
    {
        private readonly IRedisConnectionProvider _conn;
        private readonly ILogger<RedisCommentCache> _logger;
        private readonly int _maxArticles;
        private readonly int _maxComments;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        // Metrics
        private readonly Meter _meter;
        private readonly Counter<long> _hit;
        private readonly Counter<long> _miss;
        private readonly Counter<long> _evictions;
        private readonly Counter<long> _skipLarge;

        private const string LruZSet = "comments:lru";

        public RedisCommentCache(
            IRedisConnectionProvider conn,
            IConfiguration cfg,
            ILogger<RedisCommentCache> logger,
            IMeterFactory meterFactory)
        {
            _conn = conn;
            _logger = logger;
            _maxArticles = Math.Clamp(cfg.GetValue("CommentCache:MaxArticles", 30), 5, 200);
            _maxComments = Math.Max(1, cfg.GetValue("CommentCache:MaxCommentsPerArticle", 5000));

            _meter = meterFactory.Create("HappyHeadlines.Cache");
            _hit = _meter.CreateCounter<long>("cache_comment_hit");
            _miss = _meter.CreateCounter<long>("cache_comment_miss");
            _evictions = _meter.CreateCounter<long>("cache_comment_evictions");
            _skipLarge = _meter.CreateCounter<long>("cache_comment_skip_large");
        }

        private static string Key(int articleId) => $"comments:article:{articleId}";

        public async Task<(IReadOnlyList<CommentDTO.CommentReadDto>? value, bool hit)> TryGetAll(int articleId,
            CancellationToken ct)
        {
            var db = await _conn.GetDatabase();
            var raw = await db.StringGetAsync(Key(articleId));

            if (raw.HasValue)
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<CommentDTO.CommentReadDto>>(raw!, JsonOpts) ?? new();
                    _hit.Add(1);
                    await Touch(articleId, db);
                    return (list, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize cached comments ArticleId={ArticleId}", articleId);
                    await db.KeyDeleteAsync(Key(articleId));
                }
                
            }
            _miss.Add(1);
            return (null, false);

        }

        public async Task StoreAll(int articleId, IReadOnlyList<CommentDTO.CommentReadDto> list, CancellationToken ct)
        {
            if (list.Count > _maxComments)
            {
                _skipLarge.Add(1);
                return;
            }

            var db = await _conn.GetDatabase();
            var payload = JsonSerializer.SerializeToUtf8Bytes(list, JsonOpts);

            var batch = db.CreateBatch(db);
            _ = batch.StringSetAsync(Key(articleId), payload, TimeSpan.FromHours(2));
            _ = batch.SortedSetAddAsync(LruZSet, articleId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            batch.Execute();

            await EnforceLru(db);
        }

        public async Task AppendIfPresent(int articleId, CommentDTO.CommentReadDto dto, CancellationToken ct)
        {
            var db = await _conn.GetDatabase();
            var key = Key(articleId);
            var raw = await db.StringGetAsync(key);
            if (!raw.HasValue) return;

            try
            {
                var list = JsonSerializer.Deserialize<List<CommentDTO.CommentReadDto>>(raw!, JsonOpts) ?? new();
                list.Insert(0, dto);
                if (list.Count > _maxComments) list.RemoveRange(_maxComments, list.Count - _maxComments);

                var payload = JsonSerializer.SerializeToUtf8Bytes(list, JsonOpts);

                var batch = db.CreateBatch();
                _ = batch.StringSetAsync(key, payload, TimeSpan.FromHours(2));
                _ = batch.SortedSetAddAsync(LruZSet, articleId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                batch.Execute();

                await EnforceLru(db);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append cached comments ArticleId={ArticleId}", articleId);
                await db.KeyDeleteAsync(key);
            }
        }
        private async Task Touch(int articleId, IDatabase db)
        {
            await db.SortedSetAddAsync(LruZSet, articleId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await EnforceLru(db);
        }

        private async Task EnforceLru(IDatabase db)
        {
            var count = await db.SortedSetLengthAsync(LruZSet);
            if (count <= _maxArticles) return;

            var over = (int)(count - _maxArticles);
            var victims = await db.SortedSetRangeByRankAsync(LruZSet, 0, over - 1);
            if (victims.Length == 0) return;

            var batch = db.CreateBatch();
            foreach (var v in victims)
            {
                if (int.TryParse(v.ToString(), out var id)) _ = batch.KeyDeleteAsync(Key(id));
                _evictions.Add(1);

            }
            batch.Execute();
        }
    }
}
