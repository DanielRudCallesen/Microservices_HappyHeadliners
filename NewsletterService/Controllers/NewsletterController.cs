using Microsoft.AspNetCore.Mvc;
using NewsletterService.Services;

namespace NewsletterService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NewsletterController : ControllerBase
    {
        private readonly IImmediateArticleStore _store;

        public NewsletterController(IImmediateArticleStore store)
        {
            _store = store;
        }

        [HttpGet]
        public IActionResult GetImmediate([FromQuery] int max = 25)
        {
            var list = _store.GetLatest(max);
            return Ok(list);
        }
    }
}
