using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Data
{
    internal class StateModelData
    {

        public string Name { get; set; }
        public string Index { get; set; }
        public string Output { get; set; }
        public List<StateModelData> SubStatesData { get; set; }
        public List<TransitionModelData> TransitionsData { get; set; }

    }
}
