using GraphX.PCL.Common.Models;
using MasterMetrology.Models.Data;

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
