namespace Qinglong.Models.Configurations
{
    public class QinglongServiceConfiguration
    {
        public string SiteUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string TotpKey { get; set; }
        public Dictionary<string, string> Commands { get; set; }
    }
}
