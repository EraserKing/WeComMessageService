namespace Qinglong.Models
{
    public class QinglongLoginOrUserModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public QinglongLoginOrUserData data { get; set; }
    }

    public class QinglongLoginOrUserData
    {
        public string token { get; set; }
        public string lastip { get; set; }
        public string lastaddr { get; set; }
        public long lastlogon { get; set; }
        public int retries { get; set; }
        public string platform { get; set; }
        public string username { get; set; }
        public bool twoFactorActivated { get; set; }
    }
}
