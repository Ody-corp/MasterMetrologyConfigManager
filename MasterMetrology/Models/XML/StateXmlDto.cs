using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MasterMetrology.Models.XML
{
    public class StateXmlDto
    {
        [XmlAttribute("Name")]
        public string? Name { get; set; }

        [XmlAttribute("Index")]
        public string? Index { get; set; }

        [XmlAttribute("Output")]
        public string? Output { get; set; }

        //[XmlArray("Transitions")]
        [XmlArrayItem("Transition")]
        public List<TransitionXmlDto> Transitions { get; set; } = new();

        //[XmlArray("SubStates")]
        [XmlArrayItem("State")]
        public List<StateXmlDto> SubStates { get; set; } = new();
    }
}
