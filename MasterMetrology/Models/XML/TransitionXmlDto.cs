using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
