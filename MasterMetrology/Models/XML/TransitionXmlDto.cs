using System.Xml.Serialization;

namespace MasterMetrology.Models.XML
{
    public class TransitionXmlDto
    {
        [XmlAttribute("Input")]
        public string? Input { get; set; }

        [XmlAttribute("NextState")]
        public string? NextState { get; set; }
    }
}
