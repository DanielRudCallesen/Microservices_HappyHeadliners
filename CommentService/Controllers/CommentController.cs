using Microsoft.AspNetCore.Mvc;

namespace CommentService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CommentController(ILogger<CommentController> logger) : ControllerBase
    {
        private readonly ILogger<CommentController> _logger = logger;


    }
}
