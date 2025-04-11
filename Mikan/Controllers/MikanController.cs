using Microsoft.AspNetCore.Mvc;
using Mikan.Services;

namespace Mikan.Controllers
{
    // This is not working when added as an application part in the main project
    // Manually add route controller in endpoint for each method
    [Route("api/[controller]")]
    [ApiController]
    public class MikanController : ControllerBase
    {
        private readonly MikanService MikanService;

        public MikanController(MikanService mikanService)
        {
            this.MikanService = mikanService;
        }

        [HttpGet("addEpisode")]
        public async Task<ActionResult<string>> AddItemById([FromQuery] string episodeId)
        {
            try
            {
                var result = await MikanService.AddEpisodeById(episodeId);
                return result;
            }
            catch
            {
                return "Issues occurred during adding episode";
            }
        }
    }
}
