using System.Text.Json.Serialization;

namespace WeComCommon.Models
{
    public class WeComRegularMessage
    {
        [JsonPropertyName("touser")]
        public string ToUser { get; set; }

        [JsonPropertyName("agentid")]
        public ulong AgentId { get; set; }

        [JsonPropertyName("msgtype")]
        public string MsgType { get; set; }

        [JsonPropertyName("text")]
        public WeComRegularMessageText Text { get; set; }

        [JsonPropertyName("textcard")]
        public WeComRegularMessageTextCard TextCard { get; set; }

        [JsonPropertyName("duplicate_check_interval")]
        public int DuplicateCheckInterval { get; set; }

        public WeComRegularMessage()
        {

        }

        public static WeComRegularMessage CreateTextMessage(ulong agentId, string to, string content)
        {
            return new WeComRegularMessage()
            {
                AgentId = agentId,
                MsgType = "text",
                ToUser = to,
                Text = new WeComRegularMessageText()
                {
                    Content = content
                }
            };
        }

        public static WeComRegularMessage CreateTextCardMessage(ulong agentId, string to, string title, string description, string url, string btnText)
        {
            return new WeComRegularMessage()
            {
                AgentId = agentId,
                MsgType = "textcard",
                ToUser = to,
                TextCard = new WeComRegularMessageTextCard()
                {
                    Title = title,
                    Description = description,
                    Url = url,
                    BtnTxt = btnText
                }
            };
        }
    }

    public class WeComRegularMessageText
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class WeComRegularMessageTextCard
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
