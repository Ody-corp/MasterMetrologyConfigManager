using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MasterMetrology.Models.Data;

namespace MasterMetrology.Models.Visual
{
    internal class GraphVertexSection : GraphVertex
    {
        public StateModelData Section { get; set; }

        public GraphVertexSection(StateModelData section)
            : base(section)
        {
            Section = section;
        }

        public override string ToString() => Section.Name + "\n" + Section.FullIndex;
    }
}
