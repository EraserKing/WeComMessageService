using System.Text.Json.Serialization;

namespace DebtServices.Models
{
    public class WeComAccessToken
    {
        [JsonPropertyName("errcode")]
        public int ErrCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrMsg { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        public DateTime ObtainedDateTime { get; set; }
    }
}
