using System.Collections.Generic;
using System.Xml.Serialization;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Models.InnerRange
{
    [XmlRoot("Results")]
    public class CardResults
    {
        [XmlAttribute("Count")]
        public int Count { get; set; }

        [XmlAttribute("PageNumber")]
        public int PageNumber { get; set; }

        [XmlAttribute("PageSize")]
        public int PageSize { get; set; }

        [XmlElement("Card")]
        public List<Card> Cards { get; set; }
    }

    public class Card
    {
        [XmlAttribute("ID")]
        public string ID { get; set; }

        public string CardID { get; set; }

        public string Notes { get; set; }

        public string CardData { get; set; }
    }
}