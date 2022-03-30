namespace DebtServices.Models.Configurations
{
    public class DebtServiceConfiguration
    {
        public int NewListingCheckHour { get; set; }
        public int NewListingCheckMinute { get; set; }
        public int NewReleaseCheckHour { get; set; }
        public int NewReleaseCheckMinute { get; set; }
        public ulong SendByAgentId { get; set; }
    }
}
