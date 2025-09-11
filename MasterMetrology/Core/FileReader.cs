using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MasterMetrology
{
    class FileReader
    {
        public XmlDocument LoadDataFromFile(string filePath)
        {
            XmlDocument xmlFile = new XmlDocument();
            xmlFile.Load(filePath);

            return xmlFile;
        }
    }
}
