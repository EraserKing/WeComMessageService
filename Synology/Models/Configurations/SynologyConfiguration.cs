namespace Synology.Models.Configurations
{
    public class SynologyConfiguration
    {
        /// <summary>
        /// Whether the Synology service is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// DSM address, for example https://192.168.1.10:5001. When the scheme is omitted, http is used.
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Base32 TOTP secret (the key shown when enabling 2-step verification). A fresh code is generated for every login.
        /// Required when the DSM account has 2-step verification enabled.
        /// </summary>
        public string OtpSecret { get; set; } = string.Empty;

        /// <summary>
        /// Accept the self-signed certificate DSM normally serves on https://host:5001.
        /// </summary>
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>
        /// Device name used for DSM's 2-step verification device token.
        /// </summary>
        public string DeviceName { get; set; } = "WeComMessageService";

        /// <summary>
        /// WeCom agent ID that routes inbound messages to this processor.
        /// Must match the Synology app's AgentId in WeComServices:AppConfigurations.
        /// </summary>
        public ulong AgentId { get; set; } = 1000007;
    }
}
