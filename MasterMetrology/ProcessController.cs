using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Xml;

namespace MasterMetrology
{
    internal class ProcessController
    {

        private readonly NodeModelVisual _DataModelVisual;
        private Panel viewPort;
        private FileReader _fileReader;

        private XmlDocument xmlFile;

        public ProcessController(Panel viewPort) 
        {
            _DataModelVisual = new NodeModelVisual();
            _fileReader = new FileReader();
            this.viewPort = viewPort;
        }

        public void SpawnObject()
        {
            Grid objectGrid = _DataModelVisual.CreateTableData(24950, 24950, "TEST");
            viewPort.Children.Add(objectGrid);
        }

        public void LoadDataXML(string filePath)
        {
            xmlFile = _fileReader.LoadDataFromFile(filePath);
        }
    }
}
