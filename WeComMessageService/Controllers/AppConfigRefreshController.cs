using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WeComMessageService.Models;
using WeComMessageService.Utilities;

namespace WeComMessageService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppConfigRefreshController : ControllerBase
    {
        private readonly ILogger<OnDemandAzureAppConfigurationRefresher> Logger;

        private readonly OnDemandAzureAppConfigurationRefresher OnDemandAzureAppConfigurationRefresher;

        public AppConfigRefreshController(OnDemandAzureAppConfigurationRefresher onDemandAzureAppConfigurationRefresher, ILogger<OnDemandAzureAppConfigurationRefresher> logger)
        {
            OnDemandAzureAppConfigurationRefresher = onDemandAzureAppConfigurationRefresher;
            Logger = logger;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Post(SubscriptionValidationEvent[] sve)
        {
            Logger.LogInformation("Receive Refresh App Configuration Request");
            try
            {
                await OnDemandAzureAppConfigurationRefresher.RefreshAllRegisteredKeysAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to refresh app configuration");
                return Problem(ex.Message);
            }
            return Ok(new { validationResponse = sve.FirstOrDefault()?.data.validationCode });
        }

        [HttpPost("forcerefresh")]
        public async Task<IActionResult> Post()
        {
            Logger.LogInformation("Force Refresh App Configuration");
            try
            {
                await OnDemandAzureAppConfigurationRefresher.RefreshAllRegisteredKeysAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to force refresh app configuration");
                return Problem(ex.Message);
            }
            return Ok();
        }
    }
}
