using System.Text.Json.Serialization;

namespace DebtServices.Models
{
    public class WeComMessageBody
    {
        [JsonPropertyName("touser")]
        public string ToUser { get; set; }

        [JsonPropertyName("agentid")]
        public string AgentId { get; set; }

        [JsonPropertyName("msgtype")]
        public string MsgType { get; set; }

        [JsonPropertyName("textcard")]
        public WeComMessageBodyTextCard TextCard { get; set; }

        [JsonPropertyName("duplicate_check_interval")]
        public int DuplicateCheckInterval { get; set; }
    }

    public class WeComMessageBodyTextCard
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("btntxt")]
        public string BtnTxt { get; set; }
    }
}
