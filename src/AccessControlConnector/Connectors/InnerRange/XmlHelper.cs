using System;
using System.IO;
using System.Xml.Serialization;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.InnerRange
{
    public static class XmlHelper
    {
        internal static T DeserializeXml<T>(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (StringReader reader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }
    }
}