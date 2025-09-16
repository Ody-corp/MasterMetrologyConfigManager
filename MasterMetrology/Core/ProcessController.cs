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
using MasterMetrology.Models.Data;

namespace MasterMetrology
{
    internal class ProcessController
    {

        private readonly NodeModelVisual _DataModelVisual;
        private Panel viewPort;
        private FileReader _fileReader;

        private List<InputsDefModelData> inputsDefModelDatas;
        private List<OutputModelData> outputsDefModelDatas;
        private List<StateModelData> statesModelDatas;


        private string filePath;

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
            SaveOldXMLPath(filePath);
            var list = _fileReader.LoadDataFromFile(filePath);

            inputsDefModelDatas = list.InputsDefinition;
            outputsDefModelDatas = list.OutputDefinition;
            statesModelDatas = list.FullListStateModelData;
        }

        private void SaveOldXMLPath(string filePath)
        {
            this.filePath = filePath;
        }
    }
}
