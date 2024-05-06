namespace Mikan.Models.Configurations
{
    public class MikanServiceConfiguration
    {
        public string MikanToken { get; set; }
        public int ClearAfterHours { get; set; }
        public ulong SendByAgentId { get; set; }
        public string PublicHost { get; set; }
    }
}
