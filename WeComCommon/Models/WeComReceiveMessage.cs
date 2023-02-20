using System.Xml.Serialization;

namespace WeComCommon.Models
{
    [XmlRoot(DataType = "xml", ElementName = "xml", IsNullable = false, Namespace = "")]
    public partial class WeComReceiveMessage
    {
        public string ToUserName { get; set; }

        public string FromUserName { get; set; }

        public long CreateTime { get; set; }

        public string MsgType { get; set; }

        public string Content { get; set; }

        public ulong MsgId { get; set; }

        public ulong AgentID { get; set; }
    }
}
