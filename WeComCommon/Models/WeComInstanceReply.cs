using System.Xml.Serialization;

namespace WeComCommon.Models
{
    [XmlRoot(DataType = "xml", ElementName = "xml", IsNullable = false, Namespace = "")]
    public class WeComInstanceReply
    {
        public string ToUserName { get; set; }

        public string FromUserName { get; set; }

        public long CreateTime { get; set; }

        public string MsgType { get; set; }

        public string Content { get; set; }

        public WeComInstanceReply()
        {

        }

        public static WeComInstanceReply Create(string from, string to, string content)
        {
            return new WeComInstanceReply()
            {
                FromUserName = from,
                ToUserName = to,
                CreateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                MsgType = "text",
                Content = content
            };
        }
    }
}
