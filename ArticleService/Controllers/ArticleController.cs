using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ArticleController(IArticleService service, ILogger<ArticleController> logger) : ControllerBase
    {

        private readonly IArticleService _service = service;
        private readonly ILogger<ArticleController> _logger = logger;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ArticleCreateDTO dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);

            return CreatedAtAction(nameof(GetById), new { id = created.id, continent = created.Continent?.ToString() },
                created);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, [FromQuery] Continent? continent,
            [FromQuery] bool includeGlobalFallback = true, CancellationToken ct = default)
        {
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
