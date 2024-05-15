namespace Qinglong.Models
{
    public class QinglongAuthTokenModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public QinglongAuthTokenData data { get; set; }
    }

    public class QinglongAuthTokenData
    {
        public string token { get; set; }
        public string token_type { get; set; }
        public int expiration { get; set; }
    }
}
