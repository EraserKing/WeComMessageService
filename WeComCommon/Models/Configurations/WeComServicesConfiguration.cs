namespace WeComCommon.Models.Configurations
{
    public class WeComServicesConfiguration
    {
        public string AdminPushId { get; set; }

        public WeComServicesAppConfiguration[] AppConfigurations { get; set; }
    }

    public class WeComServicesAppConfiguration
    {
        public string CorpId { get; set; }
        public string CorpSecret { get; set; }
        public ulong AgentId { get; set; }
        public string AppId { get; set; }
        public WeComConfigurationReceiveMessage Message { get; set; }
    }

    public class WeComConfigurationReceiveMessage
    {
        public string Token { get; set; }
        public string EncodingAESKey { get; set; }
    }
}
