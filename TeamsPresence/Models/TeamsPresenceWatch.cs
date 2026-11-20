using System.Text.Json.Serialization;

namespace TeamsPresence.Models
{
    /// <summary>
    /// Response of GET /watch and of the watch-list edit endpoints.
    /// <see cref="Changed"/> is null for a plain read and false when an add or
    /// remove was a no-op.
    /// </summary>
    public class TeamsPresenceWatch
    {
        [JsonPropertyName("senders")]
        public List<string> Senders { get; set; } = [];

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = [];

        [JsonPropertyName("changed")]
        public bool? Changed { get; set; }
    }

    public enum TeamsPresenceWatchList
    {
        Senders,
        Keywords,
    }

    public enum TeamsPresenceWatchAction
    {
        Add,
        Remove,
    }
}
