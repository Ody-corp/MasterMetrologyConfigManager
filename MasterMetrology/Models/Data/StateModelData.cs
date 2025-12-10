using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Data
{
    public class StateModelData
    {
        public string Name { get; set; }
        public string Index { get; set; }
        public string FullIndex { get; set; }
        public string Output { get; set; }
        public StateModelData Parent { get; set; } = null;
        public ObservableCollection<StateModelData> SubStatesData { get; set; } = new();
        public ObservableCollection<TransitionModelData> TransitionsData { get; set; } = new();

        //Layout
        public double LayoutX { get; set; }
        public double LayoutY { get; set; }
    }
}
