using MasterMetrology.Models.Xml;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MasterMetrology.Controllers
{
    internal static class ProcessXmlWriter
    {
        public static void Save(string path, ProcessXmlDto dto)
        {
            var serializer = new XmlSerializer(typeof(ProcessXmlDto));

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                //NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration = false
            };

            var ns = new XmlSerializerNamespaces();
            ns.Add("", ""); // bez xmlns

            using var fs = File.Create(path);
            using var writer = XmlWriter.Create(fs, settings);
            serializer.Serialize(writer, dto, ns);
        }
    }
}
