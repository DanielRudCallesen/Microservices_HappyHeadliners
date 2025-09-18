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
        private readonly IDraftService _service = service;

        [HttpPost("snapshot")]
        public async Task<IActionResult> Snapshot([FromBody] DraftSnapshotRequest request, CancellationToken ct)
        {
            var saved = await _service.SaveSnapShotAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            var draft = await _service.GetAsync(id, ct);
            return draft is null ? NotFound() : Ok(draft);
        }

        [HttpGet("by-article/{articleId:int}")]
        public async Task<IActionResult> GetByArticle(int articleId, CancellationToken ct)
        {
            var list = await _service.GetByArticleAsync(articleId, ct);
            return Ok(list);
        }
    }
}
