namespace DebtServices.Models.Configurations
{
    public class MikanServiceConfiguration
    {
        public string MikanToken { get; set; }
        public int ClearAfterHours { get; set; }
        public string QbUrl { get; set; }
        public string QbUsername { get; set; }
        public string QbPassword { get; set; }
        public ulong SendByAgentId { get; set; }
    }
}
