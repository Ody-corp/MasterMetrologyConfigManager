using System.Collections.ObjectModel;

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