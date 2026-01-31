using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MasterMetrology.Models.XML
{
    public class InputDefXmlDto
    {
        [XmlAttribute("Name")]
        public string? Name { get; set; }

        [XmlAttribute("ID")]
        public string? ID { get; set; }
    }
}
