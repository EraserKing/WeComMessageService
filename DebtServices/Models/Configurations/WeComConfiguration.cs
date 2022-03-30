namespace DebtServices.Models.Configurations
{
    public class WeComConfiguration
    {
        public string CorpId { get; set; }
        public string CorpSecret { get; set; }
        public ulong AgentId { get; set; }
        public string AdminPushId { get; set; }

        public int NewListingCheckHour { get; set; }
        public int NewListingCheckMinute { get; set; }
        public int NewReleaseCheckHour { get; set; }
        public int NewReleaseCheckMinute { get; set; }

        public WeComConfigurationMessage Message { get; set; }
    }
    public class WeComConfigurationMessage
    {
        public string Token { get; set; }
        public string EncodingAESKey { get; set; }
    }
}
