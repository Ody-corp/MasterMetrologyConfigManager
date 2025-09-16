using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Core
{
    internal class Visual
    {
        StateModelVisual visual = new StateModelVisual();

        public void RenderStates(List<StateModelData> states, double startX = 50, double startY = 50, double spacingX = 200, double spacingY = 100)
        {

            foreach (StateModelData state in states)
            {
                RenderStateRecursive(state, startX, startY, spacingX, spacingY);
            }

        }

        private void RenderStateRecursive(StateModelData state, double x, double y, double spacingX, double spacingY)
        {
            var grid = visual.CreateTableData(x, y, state.Name, state.Index);
        }
    }
}
