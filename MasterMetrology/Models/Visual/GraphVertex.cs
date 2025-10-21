using GraphX.PCL.Common.Models;
using MasterMetrology.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MasterMetrology.Models.Visual
{
    public class GraphVertex : VertexBase
    {
        public StateModelData State { get; set; }

        public GraphVertex(StateModelData state)
        {
            State = state;
        }

        public override string ToString() => State.Name + "\n" + State.FullIndex;

    }
}
