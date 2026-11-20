using System.Text.Json.Serialization;

namespace TeamsPresence.Models
{
    /// <summary>
    /// Response of GET /status. A null value means the loop has not ticked yet
    /// or the field does not apply (for example pause_date when not paused).
    /// </summary>
    public class TeamsPresenceStatus
    {
        [JsonPropertyName("paused")]
        public bool Paused { get; set; }

        [JsonPropertyName("pause_date")]
        public string? PauseDate { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("applied")]
        public string? Applied { get; set; }

        [JsonPropertyName("window")]
        public string? Window { get; set; }

        [JsonPropertyName("cutoff")]
        public string? Cutoff { get; set; }

        [JsonPropertyName("updated")]
        public string? Updated { get; set; }
    }
}
