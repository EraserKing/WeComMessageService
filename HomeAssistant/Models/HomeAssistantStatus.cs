using System.Text.Json.Serialization;

namespace HomeAssistant.Models
{
    public class HomeAssistantStatus
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
