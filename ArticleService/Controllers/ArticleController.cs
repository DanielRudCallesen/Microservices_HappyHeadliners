using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ArticleController(IArticleService service, ILogger<ArticleController> logger, IConfiguration config) : ControllerBase
    {

        private readonly IArticleService _service = service;
        private readonly ILogger<ArticleController> _logger = logger;
        private readonly IConfiguration _config = config;

        private IEnumerable<Continent> GetEnabledShardsExcludingGlobal()
        {
            // Read shards from config (array or CSV), fallback to only Europe (common dev case), then enum (excluding Global)
            var enabled = _config.GetSection("Migrations:EnabledShards").Get<string[]>();
            if ((enabled == null || enabled.Length == 0) && !string.IsNullOrWhiteSpace(_config["Migrations:EnabledShards"]))
            {
                enabled = _config["Migrations:EnabledShards"]!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            if (enabled is { Length: > 0 })
            {
                foreach (var s in enabled)
                {
                    if (Enum.TryParse<Continent>(s, true, out var c) && c != Continent.Global)
                        yield return c;
                }
                yield break;
            }

            // Fallback: try common dev shard (Europe), then the rest
            yield return Continent.Europe;
            foreach (var c in Enum.GetValues<Continent>())
            {
                if (c != Continent.Global && c != Continent.Europe)
                    yield return c;
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, [FromQuery] Continent? continent,
            [FromQuery] bool includeGlobalFallback = true, [FromQuery] bool searchAll = false, CancellationToken ct = default)
        {
            if (searchAll)
            {
                // 1) Global first (fast path)
                var global = await _service.GetAsync(id, Continent.Global, false, ct);
                if (global is not null) return Ok(global);

                // 2) Probe only enabled shards with a short per-shard timeout
                foreach (var shard in GetEnabledShardsExcludingGlobal().Distinct())
                {
                    using var shardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    shardCts.CancelAfter(TimeSpan.FromSeconds(1)); // keep this tight to avoid DB timeouts on down shards
                    try
                    {
                        var any = await _service.GetAsync(id, shard, false, shardCts.Token);
                        if (any is not null) return Ok(any);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogDebug("Timed out probing shard {Shard} for Article {ArticleId}", shard, id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error probing shard {Shard} for Article {ArticleId}", shard, id);
                    }
                }

                return NotFound();
            }

            var found = await _service.GetAsync(id, continent, includeGlobalFallback, ct);
            return found is null ? NotFound() : Ok(found);
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] Continent? continent, [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20, [FromQuery] bool includeGlobal = true, CancellationToken ct = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var result = await _service.GetListAsync(continent, page, pageSize, includeGlobal, ct);
            return Ok(result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromQuery] Continent? continent,
            [FromBody] ArticleUpdateDTO dto, CancellationToken ct = default)
        {
            try
            {
                var success = await _service.UpdateAsync(id, continent, dto, ct);
                return success ? NoContent() : NotFound();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency issue when updating article {ArticleId} in {Continent} repository",
                    id, continent?.ToString() ?? "Global");
                return Conflict("Error. Please reload and try again.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] Continent? continent,
            CancellationToken ct = default)
        {
            try
            {
                var succuss = await _service.DeleteAsync(id, continent, ct);
                return succuss ? NoContent() : NotFound();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency issue when deleting article {ArticleId} in {Continent} repository",
                    id, continent?.ToString() ?? "Global");
                return Conflict("Error. Please reload and try again.");
            }
        }
    }
}
