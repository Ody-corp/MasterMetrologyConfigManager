using MasterMetrology.Models.XML;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MasterMetrology.Models.Xml
{
    [XmlRoot("MeasurementModule")]
    public class ProcessXmlDto
    {
        [XmlArray("InputsDefinition")]
        [XmlArrayItem("Input")]
        public List<InputDefXmlDto> Inputs { get; set; } = new();

        [XmlArray("OutputsDefinition")]
        [XmlArrayItem("Output")]
        public List<OutputXmlDto> Outputs { get; set; } = new();

        [XmlElement("StateMachine")]
        public StateMachineXmlDto StateMachine { get; set; } = new();
    }
}
