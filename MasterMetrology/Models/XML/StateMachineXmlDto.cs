using MasterMetrology.Models.XML;
using System.Xml.Serialization;

namespace MasterMetrology.Models.Xml
{
    public class StateMachineXmlDto
    {
        [XmlAttribute("Depth")]
        public string? Depth { get; set; }

        [XmlElement("State")]
        public List<StateXmlDto> States { get; set; } = new();
    }
}
