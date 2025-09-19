using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MasterMetrology.Core
{
    internal class VisualRendering(Panel viewPort)
    {
        StateModelVisual visual = new StateModelVisual();
        Panel viewPort = viewPort;
        List<Size> statesCords = new List<Size>();

        public void RenderStates(List<StateModelData> states, double startX = 25000, double startY = 25000, double spacingX = 200, double spacingY = 100)
        {

            foreach (StateModelData state in states)
            {
                var size = MeasureState(state, spacingX, spacingY);

                var grid = RenderStateRecursive(state, startX, startY, spacingX, spacingY);
                viewPort.Children.Add(grid);
                
                startY += size.Height + spacingY;
            }

        }

        private Grid RenderStateRecursive(StateModelData state, double x, double y, double spacingX, double spacingY)
        {
            Grid stateBox;
            
            if (state.SubStatesData.Count == 0)
            {
                stateBox = visual.CreateTableData(x, y, state.Name, state.Index);
                stateBox.Tag = state;
            }
            else
            {
                stateBox = visual.CreateSectionData(x, y, state.Name, state.Index);
                stateBox.Tag = state;

                var innerGrid = stateBox.Children[0] as StackPanel;

                double subY = 0; // offset v rámci parent Gridu
                foreach (var sub in state.SubStatesData)
                {
                    var subBox = visual.CreateTableData(0, subY, sub.Name, sub.Index); // pozícia v rámci innerGrid
                    subBox.Tag = sub;

                    innerGrid.Children.Add(RenderStateRecursive(sub, 0, subY, spacingX, spacingY));

                    subY += MeasureState(state, spacingX, spacingY).Height + spacingY; // medzera medzi subStates
                }

                
            }
            Debug.WriteLine(state.Name + " x: " + x + " y: " + y);
            return stateBox;
        }

        private Size MeasureState(StateModelData state, double spacingX, double spacingY)
        {
            const double baseWidth = 120;
            const double baseHeight = 60;

            if (state.SubStatesData.Count == 0)
                return new Size(baseWidth, baseHeight);

            double totalHeight = 0;
            double maxWidth = 0;

            foreach (var sub in state.SubStatesData)
            {
                var subSize = MeasureState(sub, spacingX, spacingY);
                totalHeight += subSize.Height + spacingY;
                maxWidth = Math.Max(maxWidth, subSize.Width);
            }

            totalHeight -= spacingY; // odstrániť poslednú medzeru
            return new Size(maxWidth + spacingX * 2, totalHeight + baseHeight);
        }
    }
}
