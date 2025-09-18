using Microsoft.AspNetCore.Mvc;

namespace DraftService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DraftsController : ControllerBase
    {

        private readonly ILogger<DraftsController> _logger;

        public DraftsController(ILogger<DraftsController> logger)
        {
            _logger = logger;
        }
    }
}
