using MasterMetrology.Models.Data;
using GraphX.PCL.Common.Models;
using System.ComponentModel;

namespace MasterMetrology.Models.Visual
{
    public class GraphEdge : EdgeBase<GraphVertex>, INotifyPropertyChanged
    {
        // interné skladisko transitions
        public List<TransitionModelData> Transitions { get; } = new List<TransitionModelData>();

        // Property pre binding
        private string _text;
        public string Text
        {
            get => _text;
            private set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }
        public string ToolTipText { get; set; }

        public GraphEdge(GraphVertex source, GraphVertex target, IEnumerable<TransitionModelData>? transitions = null)
            : base(source, target, 1)
        {
            if (transitions != null)
                Transitions.AddRange(transitions);
            UpdateLabelFromTransitions();
        }

        public void AddTransition(TransitionModelData t)
        {
            Transitions.Add(t);
            UpdateLabelFromTransitions();
        }

        public bool RemoveTransition(TransitionModelData t)
        {
            if (t == null) return false;
            var removed = Transitions.Remove(t);
            if (removed) UpdateLabelFromTransitions();
            return removed;
        }

        public void UpdateLabelFromTransitions()
        {
            var inputs = Transitions.Select(x => x.Input).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            Text = inputs.Length == 0 ? string.Empty : string.Join(", ", inputs);
        }

        public override string ToString()
        {
            return Text;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}