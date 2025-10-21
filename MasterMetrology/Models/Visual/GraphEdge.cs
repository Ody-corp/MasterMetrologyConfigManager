using MasterMetrology.Models.Data;
using QuickGraph;
using GraphX.PCL.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphX.PCL.Common.Interfaces;

namespace MasterMetrology.Models.Visual
{
    public class GraphEdge : EdgeBase<GraphVertex>
    {
        public TransitionModelData Transition { get; }

        // Nové — label a zoznam inputov
        public string Label { get; set; }
        public List<string> Inputs { get; } = new List<string>();

        public GraphEdge(GraphVertex source, GraphVertex target, TransitionModelData transition)
            : base(source, target, 1)
        {
            Transition = transition;
            if (transition != null && !string.IsNullOrEmpty(transition.Input))
                Inputs.Add(transition.Input);
            Label = transition?.Input ?? string.Empty;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Label)) return Label;
            return Transition?.Input ?? string.Empty;
        }
    }
}
