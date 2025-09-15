using Microsoft.AspNetCore.Mvc;

namespace ProfanityService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProfanityController : ControllerBase
    {


        private readonly ILogger<ProfanityController> _logger;

        public ProfanityController(ILogger<ProfanityController> logger)
        {
            _logger = logger;
        }
    }
}
