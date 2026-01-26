using GraphX.Controls;
using MasterMetrology.Core.GraphX;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;        // WPF Point, Rect
using System.Windows.Media;  // Vector

using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using GxPoint = GraphX.Measure.Point;

namespace MasterMetrology.Core.Rendering
{
    /// <summary>
    /// Edge routing, ktorý:
    /// - berie "cudzie" sekcie ako prekážky
    /// - ignoruje sekcie, ktoré patria zdroju alebo cieľu (chain parent sekcií)
    /// - berie leaf vertexy ako prekážky (okrem source/target)
    /// - routuje ortogonálne pomocou A* na gride
    /// - a navyše: penalizuje už použité bunky/segmenty, aby sa hrany "nerovnali na seba"
    /// </summary>
    internal sealed class SectionAwareEdgeRouter
    {
        public double CellSize { get; set; } = 30;
        public double WorldPadding { get; set; } = 250;
        public double LeafMargin { get; set; } = 14;
        public double SectionMargin { get; set; } = 18;
        public double UsedCellPenalty { get; set; } = 2;   
        public double UsedEdgePenalty { get; set; } = 4;   

        // --- caches ---
        private readonly Dictionary<string, List<string>> _containersByState = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WpfRect> _sectionRectByFull = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WpfRect> _leafRectByFull = new(StringComparer.Ordinal);


        public void RebuildCaches(List<StateModelData> roots, StateGraphArea area)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (area == null) throw new ArgumentNullException(nameof(area));

            BuildContainerChains(roots);
            BuildObstacleCaches(area);
        }

        public void RouteAllEdges(StateGraphArea area)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (area.EdgesList == null || area.EdgesList.Count == 0) return;

            var world = ComputeWorldBounds(area, WorldPadding);

            // heatmap: koľkokrát už bunka/segment použitý
            var usedCells = new Dictionary<(int x, int y), int>();
            var usedEdges = new Dictionary<(int x1, int y1, int x2, int y2), int>();

            // stabilné poradie, aby sa výsledok nemenil náhodne
            var edgesOrdered = area.EdgesList
                .Select(kv => (edge: kv.Key as GraphEdge, ec: kv.Value as EdgeControl))
                .Where(x => x.edge != null && x.ec != null)
                .OrderBy(x => (x.edge!.Source as GraphVertex)?.State?.FullIndex, StringComparer.Ordinal)
                .ThenBy(x => (x.edge!.Target as GraphVertex)?.State?.FullIndex, StringComparer.Ordinal)
                .ToList();

            foreach (var item in edgesOrdered)
            {
                var edgeObj = item.edge!;
                var ec = item.ec!;

                if (edgeObj.Source is not GraphVertex srcV || edgeObj.Target is not GraphVertex trgV) continue;

                var srcFull = srcV.State?.FullIndex;
                var trgFull = trgV.State?.FullIndex;
                if (string.IsNullOrWhiteSpace(srcFull) || string.IsNullOrWhiteSpace(trgFull)) continue;

                // allowed sections = containers(src) ∪ containers(trg)
                var allowedSections = new HashSet<string>(StringComparer.Ordinal);
                if (_containersByState.TryGetValue(srcFull, out var a))
                    foreach (var s in a) allowedSections.Add(s);
                if (_containersByState.TryGetValue(trgFull, out var b))
                    foreach (var s in b) allowedSections.Add(s);

                // obstacles pre túto hranu
                var obstacles = new List<WpfRect>(512);

                foreach (var lr in _leafRectByFull)
                {
                    if (lr.Key == srcFull || lr.Key == trgFull) continue;
                    var r = lr.Value;
                    r.Inflate(LeafMargin, LeafMargin);
                    obstacles.Add(r);
                }

                foreach (var sr in _sectionRectByFull)
                {
                    if (allowedSections.Contains(sr.Key)) continue;
                    var r = sr.Value;
                    r.Inflate(SectionMargin, SectionMargin);
                    obstacles.Add(r);
                }

                // porty (štart/cieľ na okraji rectu)
                var srcRect = GetRect(ec.Source);
                var trgRect = GetRect(ec.Target);

                var start = PickPort(srcRect, trgRect);
                var goal = PickPort(trgRect, srcRect);

                // A* s penalizáciou za už použité bunky/segmenty
                var pts = RouteAStarWithCongestion(
                    start, goal, obstacles, world, CellSize,
                    usedCells, usedEdges, UsedCellPenalty, UsedEdgePenalty);

                if (pts == null || pts.Count < 2)
                    pts = FallbackL(start, goal, obstacles);

                var rawPts = pts;
                pts = SimplifyCollinear(pts);

                // zapísať použitie buniek/segmentov (aby ďalšie hrany šli vedľa)
                //StampUsage(pts, world, CellSize, usedCells, usedEdges);
                StampUsageDense(rawPts, world, CellSize, usedCells, usedEdges);

                // uložiť do GraphX (IRoutingInfo) ako GraphX.Measure.Point[]
                edgeObj.RoutingPoints = pts.Select(p => new GxPoint(p.X, p.Y)).ToArray();
                ec.UpdateEdge();
            }
        }

        // ---------------- containers ----------------

        private static void StampUsageDense(
    List<WpfPoint> pts,
    WpfRect world,
    double cell,
    Dictionary<(int x, int y), int> usedCells,
    Dictionary<(int x1, int y1, int x2, int y2), int> usedEdges)
        {
            if (pts == null || pts.Count < 2) return;

            int ToGX(double x) => (int)Math.Round((x - world.Left) / cell);
            int ToGY(double y) => (int)Math.Round((y - world.Top) / cell);

            // pre každý segment A->B “prejdi” po grid krokoch a stampuj všetky bunky aj segmenty
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int x0 = ToGX(pts[i].X);
                int y0 = ToGY(pts[i].Y);
                int x1 = ToGX(pts[i + 1].X);
                int y1 = ToGY(pts[i + 1].Y);

                int dx = Math.Sign(x1 - x0);
                int dy = Math.Sign(y1 - y0);

                // ortogonálne segmenty => vždy ide dx==0 alebo dy==0
                int cx = x0, cy = y0;

                // stampni prvú bunku
                usedCells[(cx, cy)] = usedCells.TryGetValue((cx, cy), out var cc0) ? cc0 + 1 : 1;

                while (cx != x1 || cy != y1)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;

                    // bunka
                    usedCells[(nx, ny)] = usedCells.TryGetValue((nx, ny), out var cc) ? cc + 1 : 1;

                    // segment (normalizovaný)
                    var eKey = NormalizeEdgeKey(cx, cy, nx, ny);
                    usedEdges[eKey] = usedEdges.TryGetValue(eKey, out var ec) ? ec + 1 : 1;

                    cx = nx; cy = ny;
                }
            }
        }


        private void BuildContainerChains(List<StateModelData> roots)
        {
            _containersByState.Clear();

            var stack = new Stack<string>();

            void Walk(StateModelData m)
            {
                if (m == null || string.IsNullOrWhiteSpace(m.FullIndex)) return;

                bool isSection = m.SubStatesData != null && m.SubStatesData.Count > 0;
                if (isSection) stack.Push(m.FullIndex);

                _containersByState[m.FullIndex] = stack.Reverse().ToList();

                if (m.SubStatesData != null)
                    foreach (var ch in m.SubStatesData)
                        Walk(ch);

                if (isSection) stack.Pop();
            }

            foreach (var r in roots)
                Walk(r);
        }

        // ---------------- obstacle caches ----------------

        private void BuildObstacleCaches(StateGraphArea area)
        {
            _sectionRectByFull.Clear();
            _leafRectByFull.Clear();

            foreach (var kv in area.VertexList)
            {
                if (kv.Key is not GraphVertex gv) continue;
                var full = gv.State?.FullIndex;
                if (string.IsNullOrWhiteSpace(full)) continue;

                if (kv.Value is not VertexControl vc) continue;

                var rect = GetRect(vc);

                if (vc.GetType().Name.Contains("SectionVertexControl", StringComparison.Ordinal))
                    _sectionRectByFull[full] = rect;
                else
                    _leafRectByFull[full] = rect;
            }
        }

        private static WpfRect GetRect(VertexControl vc)
        {
            var p = vc.GetPosition();
            double w = vc.ActualWidth > 0 ? vc.ActualWidth : (vc.Width > 0 ? vc.Width : 80);
            double h = vc.ActualHeight > 0 ? vc.ActualHeight : (vc.Height > 0 ? vc.Height : 40);
            return new WpfRect(p.X, p.Y, w, h);
        }

        private static WpfRect ComputeWorldBounds(StateGraphArea area, double pad)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

            foreach (var kv in area.VertexList)
            {
                if (kv.Value is not VertexControl vc) continue;
                var r = GetRect(vc);

                minX = Math.Min(minX, r.Left);
                minY = Math.Min(minY, r.Top);
                maxX = Math.Max(maxX, r.Right);
                maxY = Math.Max(maxY, r.Bottom);
            }

            if (double.IsInfinity(minX))
                return new WpfRect(0, 0, 5000, 5000);

            return new WpfRect(minX - pad, minY - pad, (maxX - minX) + 2 * pad, (maxY - minY) + 2 * pad);
        }

        // ---------------- ports ----------------

        private static WpfPoint PickPort(WpfRect from, WpfRect to)
        {
            var fromC = new WpfPoint(from.Left + from.Width / 2, from.Top + from.Height / 2);
            var toC = new WpfPoint(to.Left + to.Width / 2, to.Top + to.Height / 2);

            double dx = toC.X - fromC.X;
            double dy = toC.Y - fromC.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                if (dx >= 0) return new WpfPoint(from.Right, fromC.Y);
                return new WpfPoint(from.Left, fromC.Y);
            }
            else
            {
                if (dy >= 0) return new WpfPoint(fromC.X, from.Bottom);
                return new WpfPoint(fromC.X, from.Top);
            }
        }

        // ---------------- routing (A* + congestion) ----------------

        private sealed record Node(int X, int Y, int Dir); // Dir: 0 none, 1 L,2 R,3 U,4 D

        private static List<WpfPoint>? RouteAStarWithCongestion(
            WpfPoint start,
            WpfPoint goal,
            List<WpfRect> obstacles,
            WpfRect world,
            double cell,
            Dictionary<(int x, int y), int> usedCells,
            Dictionary<(int x1, int y1, int x2, int y2), int> usedEdges,
            double usedCellPenalty,
            double usedEdgePenalty)
        {
            int W = (int)Math.Ceiling(world.Width / cell);
            int H = (int)Math.Ceiling(world.Height / cell);
            if (W <= 2 || H <= 2) return null;

            int ToGX(double x) => (int)Math.Round((x - world.Left) / cell);
            int ToGY(double y) => (int)Math.Round((y - world.Top) / cell);
            WpfPoint FromG(int gx, int gy) => new(world.Left + gx * cell, world.Top + gy * cell);

            int sx = Clamp(ToGX(start.X), 0, W - 1);
            int sy = Clamp(ToGY(start.Y), 0, H - 1);
            int gx2 = Clamp(ToGX(goal.X), 0, W - 1);
            int gy2 = Clamp(ToGY(goal.Y), 0, H - 1);

            bool Blocked(int x, int y)
            {
                var p = FromG(x, y);
                foreach (var r in obstacles)
                    if (r.Contains(p)) return true;
                return false;
            }

            if (Blocked(sx, sy)) (sx, sy) = FindNearestFree(sx, sy, W, H, Blocked);
            if (Blocked(gx2, gy2)) (gx2, gy2) = FindNearestFree(gx2, gy2, W, H, Blocked);

            var startN = new Node(sx, sy, 0);

            var open = new PriorityQueue<Node, double>();
            var cameFrom = new Dictionary<Node, Node>();
            var gScore = new Dictionary<Node, double> { [startN] = 0 };

            double Heur(int x, int y) => Math.Abs(x - gx2) + Math.Abs(y - gy2);

            open.Enqueue(startN, Heur(sx, sy));

            int[] dxs = { -1, 1, 0, 0 };
            int[] dys = { 0, 0, -1, 1 };
            int[] dirs = { 1, 2, 3, 4 };

            while (open.Count > 0)
            {
                var cur = open.Dequeue();
                if (cur.X == gx2 && cur.Y == gy2)
                    return ReconstructPoints(cur, cameFrom, FromG, start, goal);

                var curG = gScore[cur];

                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.X + dxs[i];
                    int ny = cur.Y + dys[i];
                    int nd = dirs[i];

                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (Blocked(nx, ny)) continue;

                    // penalizácia za odbočky
                    double turnPenalty = (cur.Dir != 0 && cur.Dir != nd) ? 0.7 : 0.0;

                    // penalizácia za "preplnenie"
                    usedCells.TryGetValue((nx, ny), out var cCount);
                    double cellPenalty = cCount * usedCellPenalty;

                    var eKey = NormalizeEdgeKey(cur.X, cur.Y, nx, ny);
                    usedEdges.TryGetValue(eKey, out var eCount);
                    double edgePenalty = eCount * usedEdgePenalty;

                    var nb = new Node(nx, ny, nd);
                    double tentative = curG + 1.0 + turnPenalty + cellPenalty + edgePenalty;

                    if (!gScore.TryGetValue(nb, out var old) || tentative < old)
                    {
                        cameFrom[nb] = cur;
                        gScore[nb] = tentative;
                        open.Enqueue(nb, tentative + Heur(nx, ny));
                    }
                }
            }

            return null;
        }

        private static (int x1, int y1, int x2, int y2) NormalizeEdgeKey(int x1, int y1, int x2, int y2)
        {
            // aby segment A->B a B->A bol rovnaký kľúč
            if (x1 < x2) return (x1, y1, x2, y2);
            if (x1 > x2) return (x2, y2, x1, y1);
            if (y1 <= y2) return (x1, y1, x2, y2);
            return (x2, y2, x1, y1);
        }

        private static void StampUsage(
            List<WpfPoint> pts,
            WpfRect world,
            double cell,
            Dictionary<(int x, int y), int> usedCells,
            Dictionary<(int x1, int y1, int x2, int y2), int> usedEdges)
        {
            if (pts == null || pts.Count < 2) return;

            int ToGX(double x) => (int)Math.Round((x - world.Left) / cell);
            int ToGY(double y) => (int)Math.Round((y - world.Top) / cell);

            int px = Clamp(ToGX(pts[0].X), 0, int.MaxValue);
            int py = Clamp(ToGY(pts[0].Y), 0, int.MaxValue);

            for (int i = 1; i < pts.Count; i++)
            {
                int nx = Clamp(ToGX(pts[i].X), 0, int.MaxValue);
                int ny = Clamp(ToGY(pts[i].Y), 0, int.MaxValue);

                // bunka
                usedCells[(nx, ny)] = usedCells.TryGetValue((nx, ny), out var cc) ? cc + 1 : 1;

                // segment
                var eKey = NormalizeEdgeKey(px, py, nx, ny);
                usedEdges[eKey] = usedEdges.TryGetValue(eKey, out var ec) ? ec + 1 : 1;

                px = nx; py = ny;
            }
        }

        private static List<WpfPoint> ReconstructPoints(Node cur, Dictionary<Node, Node> cameFrom, Func<int, int, WpfPoint> fromG, WpfPoint start, WpfPoint goal)
        {
            var path = new List<Node> { cur };
            while (cameFrom.TryGetValue(cur, out var prev))
            {
                cur = prev;
                path.Add(cur);
            }
            path.Reverse();

            var pts = new List<WpfPoint>(path.Count + 2) { start };
            foreach (var n in path)
                pts.Add(fromG(n.X, n.Y));
            pts.Add(goal);

            return pts;
        }

        private static (int, int) FindNearestFree(int x, int y, int W, int H, Func<int, int, bool> blocked)
        {
            var q = new Queue<(int x, int y)>();
            var seen = new HashSet<(int, int)>();
            q.Enqueue((x, y));
            seen.Add((x, y));

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            while (q.Count > 0)
            {
                var p = q.Dequeue();
                if (!blocked(p.x, p.y)) return p;

                for (int i = 0; i < 4; i++)
                {
                    int nx = p.x + dx[i], ny = p.y + dy[i];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (seen.Add((nx, ny))) q.Enqueue((nx, ny));
                }
            }
            return (x, y);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        // ---------------- fallback + simplify ----------------

        private static List<WpfPoint> FallbackL(WpfPoint a, WpfPoint b, List<WpfRect> obstacles)
        {
            var p1 = new WpfPoint(b.X, a.Y);
            if (!SegmentHitsAny(a, p1, obstacles) && !SegmentHitsAny(p1, b, obstacles))
                return new List<WpfPoint> { a, p1, b };

            var p2 = new WpfPoint(a.X, b.Y);
            if (!SegmentHitsAny(a, p2, obstacles) && !SegmentHitsAny(p2, b, obstacles))
                return new List<WpfPoint> { a, p2, b };

            return new List<WpfPoint> { a, b };
        }

        private static bool SegmentHitsAny(WpfPoint a, WpfPoint b, List<WpfRect> obstacles)
        {
            foreach (var r in obstacles)
            {
                if (r.Contains(a) || r.Contains(b)) return true;
                if (LineIntersectsRect(a, b, r)) return true;
            }
            return false;
        }

        private static bool LineIntersectsRect(WpfPoint a, WpfPoint b, WpfRect r)
        {
            if (a.X < r.Left && b.X < r.Left) return false;
            if (a.X > r.Right && b.X > r.Right) return false;
            if (a.Y < r.Top && b.Y < r.Top) return false;
            if (a.Y > r.Bottom && b.Y > r.Bottom) return false;

            var p1 = new WpfPoint(r.Left, r.Top);
            var p2 = new WpfPoint(r.Right, r.Top);
            var p3 = new WpfPoint(r.Right, r.Bottom);
            var p4 = new WpfPoint(r.Left, r.Bottom);

            return LinesIntersect(a, b, p1, p2) ||
                   LinesIntersect(a, b, p2, p3) ||
                   LinesIntersect(a, b, p3, p4) ||
                   LinesIntersect(a, b, p4, p1);
        }

        private static bool LinesIntersect(WpfPoint a1, WpfPoint a2, WpfPoint b1, WpfPoint b2)
        {
            static double Cross(Vector v, Vector w) => v.X * w.Y - v.Y * w.X;

            var r = a2 - a1;
            var s = b2 - b1;
            var qp = b1 - a1;

            var rxs = Cross(r, s);
            var qpxr = Cross(qp, r);

            if (Math.Abs(rxs) < 1e-9 && Math.Abs(qpxr) < 1e-9) return false;
            if (Math.Abs(rxs) < 1e-9) return false;

            var t = Cross(qp, s) / rxs;
            var u = Cross(qp, r) / rxs;

            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }

        private static List<WpfPoint> SimplifyCollinear(List<WpfPoint> pts)
        {
            if (pts == null || pts.Count < 3) return pts;

            var outPts = new List<WpfPoint> { pts[0] };

            for (int i = 1; i < pts.Count - 1; i++)
            {
                var a = outPts[^1];
                var b = pts[i];
                var c = pts[i + 1];

                bool collinear =
                    (Math.Abs(a.X - b.X) < 0.01 && Math.Abs(b.X - c.X) < 0.01) ||
                    (Math.Abs(a.Y - b.Y) < 0.01 && Math.Abs(b.Y - c.Y) < 0.01);

                if (!collinear) outPts.Add(b);
            }

            outPts.Add(pts[^1]);
            return outPts;
        }
    }
}
