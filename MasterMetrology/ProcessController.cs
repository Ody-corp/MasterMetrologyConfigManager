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

namespace MasterMetrology
{
    internal class ProcessController
    {

        private DataModelVisual _DataModelVisual;
        private Panel viewPort;

        public ProcessController(Panel viewPort) 
        {
            _DataModelVisual = new DataModelVisual();
            this.viewPort = viewPort;
        }

        public void SpawnObject()
        {
            Grid objectGrid = _DataModelVisual.CreateTableData(24950, 24950, "TEST");
            viewPort.Children.Add(objectGrid);
        }
    }
}
