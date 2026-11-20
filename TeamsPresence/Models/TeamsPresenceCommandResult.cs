using System.Text.Json.Serialization;

namespace TeamsPresence.Models
{
    /// <summary>
    /// Response of POST /pause and POST /resume.
    /// </summary>
    public class TeamsPresencePauseResult
    {
        [JsonPropertyName("paused")]
        public bool Paused { get; set; }
    }

    /// <summary>
    /// Response of POST /status/&lt;name&gt;.
    /// </summary>
    public class TeamsPresenceManualResult
    {
        [JsonPropertyName("manual")]
        public string? Manual { get; set; }
    }
}
