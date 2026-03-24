using MasterMetrology.Models.Data;
using MasterMetrology.Models.Xml;
using MasterMetrology.Models.XML;
using QuickGraph.Algorithms.Search;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MasterMetrology.Core
{
    internal static class ProcessXmlExporter
    {
        public static ProcessXmlDto Build(
            ObservableCollection<InputModelData> inputs,
            ObservableCollection<OutputModelData> outputs,
            List<StateModelData> roots)
        {
            var states = roots?.Select(MapStateRecursive).ToList() ?? new List<StateXmlDto>();
            var depth = ComputeMaxDepth(roots);

            return new ProcessXmlDto
            {
                Inputs = inputs?.Select(MapInput).ToList() ?? new(),
                Outputs = outputs?.Select(MapOutput).ToList() ?? new(),
                StateMachine = new StateMachineXmlDto
                {
                    Depth = depth.ToString(),
                    States = states
                }
            };
        }

        private static InputDefXmlDto MapInput(InputModelData m)
        {
            return new InputDefXmlDto
            {
                Name = m.Name,
                ID = m.ID
            };
        }

        private static OutputXmlDto MapOutput(OutputModelData m)
        {
            return new OutputXmlDto
            {
                Name = m.Name,
                ID = m.ID,
                UpdateCalibration = m.UpdateCalibration.ToString(),
                UpdateDefition = m.UpdateDefinition.ToString(),
                UpdateMeasuredData = m.UpdateMeasuredData.ToString(),
                UpdateParameters = m.UpdateParameters.ToString(),
                UpdateProcessedData = m.UpdateProcessedData.ToString()
            };
        }

        private static StateXmlDto MapStateRecursive(StateModelData s)
        {
            var dto = new StateXmlDto
            {
                Name = s.Name,
                Index = s.Index,
                Output = s.Output
            };

            if (s.TransitionsData != null)
            {
                dto.Transitions = s.TransitionsData.Select(t => new TransitionXmlDto
                {
                    Input = t.Input,
                    NextState = t.NextState.FullIndex
                }).ToList();
            }

            if (s.SubStatesData != null && s.SubStatesData.Count > 0)
            {
                dto.SubStates = s.SubStatesData.Select(MapStateRecursive).ToList();
            }

            return dto;
        }

        private static int ComputeMaxDepth(List<StateModelData>? roots)
        {
            if (roots == null || roots.Count == 0) return 0;

            int max = 0;

            int depthOf(StateModelData s)
            {
                // depth počítame podľa uzlov v ceste: root = 1
                if (s.SubStatesData == null || s.SubStatesData.Count == 0) return 1;
                int childMax = 0;
                foreach (var ch in s.SubStatesData)
                    childMax = Math.Max(childMax, depthOf(ch));
                return 1 + childMax;
            }

            foreach (var r in roots)
                max = Math.Max(max, depthOf(r));

            return max;
        }

    }
}
