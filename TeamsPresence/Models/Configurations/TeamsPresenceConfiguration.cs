namespace TeamsPresence.Models.Configurations
{
    public class TeamsPresenceConfiguration
    {
        /// <summary>
        /// Whether the Teams presence control service is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Address of the teams-ctl API, for example http://192.168.2.6:40681.
        /// </summary>
        public string BaseUrl { get; set; } = "http://192.168.2.6:40681";

        /// <summary>
        /// Control token from state/ctl-token on the teams-presence host.
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}
