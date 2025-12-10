using MasterMetrology.Models.Data;

namespace MasterMetrology.Models.Visual
{
    public class GraphVertexSection : GraphVertex
    {
        public StateModelData Section { get; set; }
        public List<GraphVertex> SubVertices { get; set; } = new();
        public System.Windows.Point LastPosition { get; set; }

        public GraphVertexSection(StateModelData section)
            : base(section)
        {
            Section = section;
        }

        public override string ToString() => "[SECTION] " + Section.Name + "\n" + Section.FullIndex;
    }
}
