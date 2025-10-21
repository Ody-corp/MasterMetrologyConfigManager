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
using MasterMetrology.Core.Rendering;

namespace MasterMetrology
{
    internal class ProcessController(Canvas viewPort)
    {

        private readonly StateModelVisual _DataModelVisual = new StateModelVisual();
        private Canvas viewPort = viewPort;
        private FileReader _fileReader = new FileReader();
        private VisualRendering visualRender = new VisualRendering();

        private List<InputsDefModelData> inputsDefModelDatas;
        private List<OutputModelData> outputsDefModelDatas;
        private List<StateModelData> statesModelDatas;


        private string filePath;

        public void SpawnObject()
        {
            Grid objectGrid = _DataModelVisual.CreateTableData(24950, 24950, "TEST", "1");
            viewPort.Children.Add(objectGrid);
        }

        public void LoadDataXML(string filePath)
        {
            SaveOldXMLPath(filePath);
            var list = _fileReader.LoadDataFromFile(filePath);

            inputsDefModelDatas = list.InputsDefinition;
            outputsDefModelDatas = list.OutputDefinition;
            statesModelDatas = list.FullListStateModelData;

            visualRender.RenderGraph(statesModelDatas, viewPort);
        }

        private void SaveOldXMLPath(string filePath)
        {
            this.filePath = filePath;
        }
    }
}
