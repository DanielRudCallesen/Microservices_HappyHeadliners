using DraftService.Interfaces;
using DraftService.Models;
using Microsoft.AspNetCore.Mvc;

namespace DraftService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DraftsController(IDraftService service, ILogger<DraftsController> logger) : ControllerBase
    {

        private readonly ILogger<DraftsController> _logger;

        [HttpPost("snapshot")]
        public async Task<IActionResult> Snapshot([FromBody] DraftSnapshotRequest request, CancellationToken ct)
        {
            var saved = await service.SaveSnapshotAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            var draft = await service.GetAsync(id, ct);
            return draft is null ? NotFound() : Ok(draft);
        }

        [HttpGet("by-article/{articleId:int}")]
        public async Task<IActionResult> GetByArticle(int articleId, CancellationToken ct)
        {
            var list = await service.GetByArticleAsync(articleId, ct);
            return Ok(list);
        }
    }
}
