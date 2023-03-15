using Microsoft.AspNetCore.Mvc;
using SmartFace.Integrations.MockAPI.Models;

namespace SmartFace.Integrations.MockAPI.Controllers
{

    [ApiController]
    [Route("/api/v1/Watchlists")]
    public class WatchlistsSearchController : ControllerBase
    {
        private readonly ILogger<WatchlistsSearchController> _logger;

        public WatchlistsSearchController(ILogger<WatchlistsSearchController> logger)
        {
            _logger = logger;
        }

        [Route("Search")]
        [HttpPost]
        public async Task<IActionResult> SearchAsync(RequestPayload payload)
        {

            return Ok(new
            {
                message = $"Image has {payload.Image?.Data?.Length} bytes"
            });
        }
    }

}