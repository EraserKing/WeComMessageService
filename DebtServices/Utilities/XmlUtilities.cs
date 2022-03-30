using DebtServices.Models;
using System.Xml.Serialization;
using System.Xml;

namespace DebtServices.Utilities
{
    public static class XmlUtilities
    {
        public static string Serialize<T>(T serializeObject)
        {
            XmlSerializerNamespaces XmlSerializerNamespaces = new XmlSerializerNamespaces();
            XmlSerializerNamespaces.Add("", "");
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.OmitXmlDeclaration = true;

            StringWriter stringWriter = new StringWriter();
            XmlWriter xmlWriter = XmlWriter.Create(stringWriter, xmlWriterSettings);

            new XmlSerializer(typeof(WeComInstanceReply)).Serialize(xmlWriter, serializeObject, XmlSerializerNamespaces);
            string replyBodyString = stringWriter.ToString();
            return replyBodyString;
        }

        public static T Deserialize<T>(string deserializeString) where T : class
        {
            return new XmlSerializer(typeof(T), "").Deserialize(new StringReader(deserializeString)) as T;
        }
    }
}
