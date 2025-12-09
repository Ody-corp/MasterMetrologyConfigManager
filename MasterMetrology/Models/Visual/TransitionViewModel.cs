using MasterMetrology.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Visual
{
    internal class TransitionViewModel
    {
        public TransitionModelData Transition { get; }
        public string FromFullIndex { get; }
        public string DisplayText { get; }
        public string Id { get; }

        public TransitionViewModel(TransitionModelData transition, string fromFullIndex)
        {
            Transition = transition;
            FromFullIndex = fromFullIndex ?? "";
            Id = $"{FromFullIndex}_{transition.NextState.FullIndex}_{transition.Input}";
            DisplayText = $"{Short(transition.FromState.FullIndex)} → {Short(transition.NextState.FullIndex)}  Input:({transition.Input})";
        }

        string Short(string s) => string.IsNullOrEmpty(s) ? "(?)" : s;

        public override string ToString() => DisplayText;
    }
}
