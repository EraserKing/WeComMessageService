namespace Qinglong.Models.Configurations
{
    public class QinglongServiceConfiguration
    {
        public string SiteUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public Dictionary<string, string> Commands { get; set; }
    }
}
