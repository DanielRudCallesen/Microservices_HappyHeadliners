using Microsoft.AspNetCore.Mvc;

namespace ProfanityService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProfanityController(ILogger<ProfanityController> logger) : ControllerBase
    {
        private readonly ILogger<ProfanityController> _logger = logger;


    }
}
