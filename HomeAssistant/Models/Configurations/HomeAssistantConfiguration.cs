namespace HomeAssistant.Models.Configurations
{
    public class HomeAssistantConfiguration
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }
}