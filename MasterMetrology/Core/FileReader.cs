using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MasterMetrology
{
    internal class FileReader
    {
        public void LoadDataFromFile(string filePath)
        {
            using (XmlReader reader = XmlReader.Create(filePath))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "xml" ||  reader.Name == "MeasurementModule")
                        {

                        }
                        else
                        {
                            //incorrect content of file 
                        }
                    }
                    else
                    {
                        //incorrect content of file
                    }
                }
            }

            
        }

        public void readData(XmlDocument xmlFile)
        {
            using (XmlReader reader = new XmlNodeReader(xmlFile)) 
            {
                
            }
        }
    }
}
