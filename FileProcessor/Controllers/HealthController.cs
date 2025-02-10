using Microsoft.AspNetCore.Mvc;

namespace FileProcessor.Controllers
{
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok("Healthy");
        }
    }
}
