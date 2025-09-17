using CommentService.Interface;
using CommentService.Models;
using Microsoft.AspNetCore.Mvc;

namespace CommentService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CommentController(ICommentService service, ILogger<CommentController> logger) : ControllerBase
    {
        private readonly ILogger<CommentController> _logger = logger;
        private readonly ICommentService _service = service;

        [HttpPost]
        public async Task<ActionResult<CommentDTO.CommentReadDto>> Create([FromBody] CommentDTO.CommentCreateDto dto,
            CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CommentDTO.CommentReadDto>> GetById([FromRoute] int id, CancellationToken ct)
        {
            var found = await _service.GetAsync(id, ct);
            return found is null ? NotFound() : Ok(found);
        }

        [HttpGet("by-article/{articleId:int}")]
        public async Task<ActionResult<CommentDTO.PageResult<CommentDTO.CommentReadDto>>> GetByArticle([FromRoute] int articleId, [FromQuery] int page =1, [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var result = await _service.GetByArticleAsync(articleId, page, pageSize, ct);
            return Ok(result);
        }
    }
}
