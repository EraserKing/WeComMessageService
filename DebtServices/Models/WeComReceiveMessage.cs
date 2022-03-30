using System.Xml.Serialization;

namespace DebtServices.Models
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
