using System.Collections.ObjectModel;
using System.Windows;

namespace MasterMetrology.Models.Data
{
    public class TransitionModelData
    {
        public string Input {  get; set; }
        public string NextStateId { get; set; }
        public StateModelData NextState { get; set; }
        public StateModelData FromState { get; set; } 
        public ObservableCollection<Point> PathPoints { get; set; } = new(); 
    }
}
