using GraphX;
using GraphX.Controls;
using GraphX.Controls.Models;
using GraphX.PCL.Common.Interfaces;
using MasterMetrology.Models.Visual;
using System.Windows;

namespace MasterMetrology.Core.GraphX.Controls
{
    public class CustomControlFactory : IGraphControlFactory
    {
        public GraphAreaBase FactoryRootArea { get; set; }

        public VertexControl CreateVertexControl(object vertex)
        {
            if (vertex is GraphVertexSection section)
                return new SectionVertexControl(section);
            else if (vertex is GraphVertex state)
                return new SimpleVertexControl(state);

            return new VertexControl(vertex);

        }

        public EdgeControl CreateEdgeControl(VertexControl source, VertexControl target, object edge, bool showArrows = true, Visibility visibility = Visibility.Visible)
        {
            //if (edge is GraphEdge ge)
            //{
            //    var ec = new LabeledEdgeControl(source, target, ge) { Visibility = visibility };
            //    ec.ShowArrows = showArrows;
            // /   return ec;
            //}

            var fallback = new EdgeControl(source, target, edge) { Visibility = visibility };
            fallback.ShowArrows = showArrows;
            return fallback;
        }
    }
}