using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MasterMetrology.Models.XML
{
    public class OutputXmlDto
    {
        [XmlAttribute("ID")]
        public string? ID { get; set; }

        [XmlAttribute("Name")]
        public string? Name { get; set; }

        [XmlAttribute("UpdateDefinition")]
        public string? UpdateDefition { get; set; }

        [XmlAttribute("UpdateParameters")]
        public string? UpdateParameters { get; set; }

        [XmlAttribute("UpdateCalibration")]
        public string? UpdateCalibration { get; set; }

        [XmlAttribute("UpdateMeasuredData")]
        public string? UpdateMeasuredData { get; set; }

        [XmlAttribute("UpdateProcessedData")]
        public string? UpdateProcessedData { get; set; }
    }
}
