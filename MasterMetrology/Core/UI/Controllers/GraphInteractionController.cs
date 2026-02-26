using GraphX.Controls;
using MasterMetrology.Core.GraphX;
using MasterMetrology.Core.UI.Rendering;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MasterMetrology.Core.UI.Controllers
{
    internal sealed class GraphInteractionController
    {
        //private readonly DispatcherTimer _rerouteTimer;
        private bool _reroutePending;

        private StateGraphArea _area;
        private List<StateModelData> _roots;
        private SectionAwareEdgeRouter _router;

        // drag tracking
        private readonly Dictionary<VertexControl, Point> _lastPos = new();
        private readonly HashSet<VertexControl> _dragging = new();

        public void Attach(StateGraphArea area, List<StateModelData> roots, SectionAwareEdgeRouter router)
        {
            _area = area ?? throw new ArgumentNullException(nameof(area));
            _roots = roots ?? throw new ArgumentNullException(nameof(roots));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            foreach (var vc in _area.VertexList.Values)
                HookVertex(vc);
        }

        private void HookVertex(VertexControl vc)
        {
            vc.PreviewMouseLeftButtonDown += OnDown;
            vc.PreviewMouseMove += OnMove;
            vc.PreviewMouseLeftButtonUp += OnUp;
            vc.LostMouseCapture += OnLostCapture;
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not VertexControl vc) return;

            _dragging.Add(vc);
            _lastPos[vc] = vc.GetPosition(final: false);
        }

        private void OnLostCapture(object sender, MouseEventArgs e)
        {
            if (sender is not VertexControl vc) return;
            FinishDrag(vc);
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not VertexControl vc) return;
            FinishDrag(vc);
        }

        private void FinishDrag(VertexControl vc)
        {
            if (_dragging.Remove(vc))
            {
                _lastPos.Remove(vc);

                RequestReroute(immediate: true);
            }
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (sender is not VertexControl vc) return;
            if (!_dragging.Contains(vc)) return;

            var cur = vc.GetPosition(final: false);
            if (!_lastPos.TryGetValue(vc, out var prev))
            {
                _lastPos[vc] = cur;
                return;
            }

            var dx = cur.X - prev.X;
            var dy = cur.Y - prev.Y;

            if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
                return;

            _lastPos[vc] = cur;

            // AK je to sekcia -> posuň jej subStates spolu
            if (IsSectionVC(vc))
            {
                if (vc.Vertex is GraphVertexSection sectionVertex)
                    MoveSectionChildren(sectionVertex, dx, dy);
            }

            // reroute počas drag (throttle)
            RequestReroute(immediate: false);
        }

        private static bool IsSectionVC(VertexControl vc)
            => vc.GetType().Name.Contains("SectionVertexControl", StringComparison.Ordinal);

        private void MoveSectionChildren(GraphVertexSection section, double dx, double dy)
        {
            // rekurzívne cez všetky subStates (aj nested sekcie)
            var stack = new Stack<GraphVertex>();
            foreach (var sv in section.SubVertices.OfType<GraphVertex>())
                stack.Push(sv);

            while (stack.Count > 0)
            {
                var v = stack.Pop();

                if (_area.VertexList.TryGetValue(v, out var childVc))
                {
                    var p = childVc.GetPosition(final: false);
                    childVc.SetPosition(p.X + dx, p.Y + dy);
                }

                if (v is GraphVertexSection sec2)
                {
                    foreach (var sub in sec2.SubVertices.OfType<GraphVertex>())
                        stack.Push(sub);
                }
            }
        }

        private void RequestReroute(bool immediate)
        {
            _reroutePending = true;

            if (immediate)
            {
                DoReroute();
                return;
            }
        }

        private void DoReroute()
        {
            if (_area == null || _router == null || _roots == null) return;

            _reroutePending = false;

            try
            {
                _area.UpdateLayout();

                _router.RebuildCaches(_roots, _area);

                _router.RouteAllEdges(_area);

                _area.UpdateLayout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DoReroute failed: " + ex.Message);
            }
        }
    }
}
