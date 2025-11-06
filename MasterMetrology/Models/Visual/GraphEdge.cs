using MasterMetrology.Models.Data;
using QuickGraph;
using GraphX.PCL.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphX.PCL.Common.Interfaces;
using System.ComponentModel;

namespace MasterMetrology.Models.Visual
{
    public class GraphEdge : EdgeBase<GraphVertex>, INotifyPropertyChanged
    {
        /*public List<TransitionModelData> Transitions { get; } = new List<TransitionModelData>();

        // Nové — label a zoznam inputov
        public string Label { get; set; }
        public List<string> Inputs => Transitions.Select(t => t.Input).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        public GraphEdge(GraphVertex source, GraphVertex target, IEnumerable<TransitionModelData>? transitions = null)
            : base(source, target, 1)
        {
            if (transitions != null)
                Transitions.AddRange(transitions);
            UpdateLabel();
        }

        public void AddTransition(TransitionModelData t)
        {
            if (t == null) return;
            Transitions.Add(t);
            UpdateLabel();
        }

        public bool RemoveTransition(TransitionModelData t)
        {
            if (t == null) return false;
            var removed = Transitions.Remove(t);
            if (removed) UpdateLabel();
            return removed;
        }
        private void UpdateLabel()
        {
            Label = string.Join(", ", Inputs);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Label) ? base.ToString() : Label;
        }*/
        // interné skladisko transitions
        public List<TransitionModelData> Transitions { get; } = new List<TransitionModelData>();

        // Property pre binding
        private string _label = string.Empty;
        public string Label
        {
            get => _label;
            private set
            {
                if (_label != value)
                {
                    _label = value;
                    OnPropertyChanged(nameof(Label));
                }
            }
        }

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
            // zložíme text labelu z inputs (prípadne iné pravidlo)
            var inputs = Transitions.Select(x => x.Input).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            Label = inputs.Length == 0 ? string.Empty : string.Join(", ", inputs);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Label) ? base.ToString() : Label;
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
