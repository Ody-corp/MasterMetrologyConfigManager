using GraphX.Controls;
using MasterMetrology.Core.GraphX;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;        // WPF Point, Rect
using System.Windows.Media;  // Vector

using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using GxPoint = GraphX.Measure.Point;

namespace MasterMetrology.Core.UI.Rendering
{
    internal sealed class SectionAwareEdgeRouter
    {
        public double CellSize { get; set; } = 40;
        public double WorldPadding { get; set; } = 150;
        public double LeafMargin { get; set; } = 20;
        public double SectionMargin { get; set; } = 20;
        public double UsedCellPenalty { get; set; } = 5;
        public double UsedEdgePenalty { get; set; } = 10;

        // ===== DEBUG / TRACE =====
        public bool EnableTrace { get; set; } = true;

        // ak edge prepočet trvá viac -> vypíš detail
        public int SlowEdgeMsThreshold { get; set; } = 25;
        public int SlowTotalMsThreshold { get; set; } = 80;

        // ak A* expandne viac -> vypíš detail
        public int AStarExpandLogThreshold { get; set; } = 1500;

        // safety: aby ti A* nevyžral UI thread donekonečna
        public int AStarMaxExpansions { get; set; } = 25000;

        private int _routeRunId = 0;

        private static string R(WpfRect r) => $"({r.Left:0},{r.Top:0},{r.Width:0},{r.Height:0})";
        private static string P(WpfPoint p) => $"({p.X:0},{p.Y:0})";

        private void Log(string msg)
        {
            if (!EnableTrace) return;
            Debug.WriteLine(msg);
        }

        private static long Ms(Stopwatch sw) => (long)sw.Elapsed.TotalMilliseconds;

        // --- caches ---
        private readonly Dictionary<string, List<string>> _containersByState = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WpfRect> _sectionRectByFull = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WpfRect> _leafRectByFull = new(StringComparer.Ordinal);

        public void RebuildCaches(List<StateModelData> roots, StateGraphArea area)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (area == null) throw new ArgumentNullException(nameof(area));

            var sw = Stopwatch.StartNew();
            Log($"[Route] RebuildCaches START roots={roots.Count} verts={area.VertexList?.Count ?? 0}");

            var sw1 = Stopwatch.StartNew();
            BuildContainerChains(roots);
            Log($"[Route] BuildContainerChains ms={Ms(sw1)} states={_containersByState.Count}");

            var sw2 = Stopwatch.StartNew();
            BuildObstacleCaches(area);
            Log($"[Route] BuildObstacleCaches ms={Ms(sw2)} sections={_sectionRectByFull.Count} leafs={_leafRectByFull.Count}");

            Log($"[Route] RebuildCaches END totalMs={Ms(sw)}");
        }

        public void RouteAllEdges(StateGraphArea area)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (area.EdgesList == null || area.EdgesList.Count == 0) return;

            _routeRunId++;
            var runId = _routeRunId;

            var swAll = Stopwatch.StartNew();
            Log($"[Route] RUN#{runId} START edges={area.EdgesList.Count}");

            var swWorld = Stopwatch.StartNew();
            var world = ComputeWorldBounds(area, WorldPadding);
            Log($"[Route] RUN#{runId} world={R(world)} cell={CellSize} worldPad={WorldPadding} ms={Ms(swWorld)}");

            // heatmap
            var usedCells = new Dictionary<(int x, int y), int>();
            var usedEdges = new Dictionary<(int x1, int y1, int x2, int y2), int>();

            var swOrder = Stopwatch.StartNew();
            var edgesOrdered = area.EdgesList
                .Select(kv => (edge: kv.Key as GraphEdge, ec: kv.Value as EdgeControl))
                .Where(x => x.edge != null && x.ec != null)
                .OrderBy(x => (x.edge!.Source as GraphVertex)?.State?.FullIndex, StringComparer.Ordinal)
                .ThenBy(x => (x.edge!.Target as GraphVertex)?.State?.FullIndex, StringComparer.Ordinal)
                .ToList();
            Log($"[Route] RUN#{runId} order ms={Ms(swOrder)} count={edgesOrdered.Count}");

            int idx = 0;
            int slowEdges = 0;

            foreach (var item in edgesOrdered)
            {
                idx++;
                var edgeObj = item.edge!;
                var ec = item.ec!;

                if (edgeObj.Source is not GraphVertex srcV || edgeObj.Target is not GraphVertex trgV) continue;

                var srcFull = srcV.State?.FullIndex;
                var trgFull = trgV.State?.FullIndex;
                if (string.IsNullOrWhiteSpace(srcFull) || string.IsNullOrWhiteSpace(trgFull)) continue;

                var swEdge = Stopwatch.StartNew();
                Log($"[Route] RUN#{runId} EDGE#{idx}/{edgesOrdered.Count} START {srcFull} -> {trgFull}");

                // allowed sections
                var swAllowed = Stopwatch.StartNew();
                var allowedSections = new HashSet<string>(StringComparer.Ordinal);
                if (_containersByState.TryGetValue(srcFull, out var a))
                    foreach (var s in a) allowedSections.Add(s);
                if (_containersByState.TryGetValue(trgFull, out var b))
                    foreach (var s in b) allowedSections.Add(s);

                Log($"[Route] allowedSections={allowedSections.Count} ms={Ms(swAllowed)}");

                // obstacles
                var swObs = Stopwatch.StartNew();
                var obstacles = new List<WpfRect>(1024);

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

                const double PORT_OUT = 10;
                const double LEAD = 24;
                const double LEAD_OUT = 30;

                // rects
                var srcRect = GetRect(ec.Source);
                var trgRect = GetRect(ec.Target);

                // block own rects a tiny bit
                var sBlock = srcRect; sBlock.Inflate(1, 1);
                var tBlock = trgRect; tBlock.Inflate(1, 1);
                obstacles.Add(sBlock);
                obstacles.Add(tBlock);

                Log($"[Route] obstacles={obstacles.Count} srcRect={R(srcRect)} trgRect={R(trgRect)} ms={Ms(swObs)}");

                // ports + leads
                var swPorts = Stopwatch.StartNew();

                var start = PickPort(srcRect, trgRect);
                var goal = PickPort(trgRect, srcRect);

                start = PushOutFromRect(srcRect, start, trgRect, PORT_OUT);
                goal = PushOutFromRect(trgRect, goal, srcRect, PORT_OUT);

                var startLead = PushOutFromRect(srcRect, start, trgRect, LEAD);
                var goalLead = PushOutFromRect(trgRect, goal, srcRect, LEAD);

                Log($"[Route] ports ms={Ms(swPorts)} start={P(start)} goal={P(goal)} startLead={P(startLead)} goalLead={P(goalLead)}");

                // A*
                var swA = Stopwatch.StartNew();
                var pts = RouteAStarWithCongestion(
                    startLead, goalLead, obstacles, world, CellSize,
                    usedCells, usedEdges, UsedCellPenalty, UsedEdgePenalty,
                    runId, idx, srcFull, trgFull);
                var aMs = Ms(swA);
                Log($"[Route] A* ms={aMs} pts={pts?.Count ?? 0}");

                if (pts == null || pts.Count < 2)
                {
                    var swFb = Stopwatch.StartNew();
                    pts = FallbackL(startLead, goalLead, obstacles);
                    Log($"[Route] fallback ms={Ms(swFb)} pts={pts.Count}");
                }

                var rawPts = pts;

                // trim endpoints (momentálne máš while vypnuté — aspoň logni, či vôbec sú body v recte)
                var swTrim = Stopwatch.StartNew();
                TrimEndpointsInsideRects(pts, srcRect, trgRect);
                Log($"[Route] trim ms={Ms(swTrim)} pts={pts.Count}");

                // simplify
                var swS = Stopwatch.StartNew();
                pts = RemoveNearDuplicates(pts, 0.5);
                //pts = SimplifyCollinear(pts);
                Log($"[Route] simplify ms={Ms(swS)} pts={pts.Count}");

                // dogleg
                var swDog = Stopwatch.StartNew();
                var len = PolylineLength(pts);
                if (len < 120)
                {
                    pts = ForceDoglegIfTooShort(pts, obstacles, CellSize * 2.0);
                    pts = SimplifyCollinear(RemoveNearDuplicates(pts, 0.5));
                }
                Log($"[Route] dogleg ms={Ms(swDog)} len={len:0} pts={pts.Count}");

                // stamp usage (toto často vie byť “tichý žrút”)
                var swStamp = Stopwatch.StartNew();
                StampUsage(pts, world, CellSize, usedCells, usedEdges);
                StampUsageDense(rawPts, world, CellSize, usedCells, usedEdges);
                Log($"[Route] stamp ms={Ms(swStamp)} usedCells={usedCells.Count} usedEdges={usedEdges.Count}");

                // apply ports (tu sa často vytvoria nové segmenty čo kolidujú)
                var swAp = Stopwatch.StartNew();
                ApplyDirectedPorts(pts, srcRect, trgRect, PORT_OUT, LEAD_OUT);
                Log($"[Route] ApplyDirectedPorts ms={Ms(swAp)} pts={pts.Count}");

                // update edge (UI) – môže byť drahé
                var swUpd = Stopwatch.StartNew();
                edgeObj.RoutingPoints = pts.Select(p => new GxPoint(p.X, p.Y)).ToArray();
                ec.UpdateEdge();
                Log($"[Route] UpdateEdge ms={Ms(swUpd)}");

                var eMs = Ms(swEdge);
                if (eMs >= SlowEdgeMsThreshold)
                {
                    slowEdges++;
                    Log($"[Route] RUN#{runId} EDGE#{idx} SLOW totalMs={eMs} A*={aMs} pts={pts.Count} obstacles={obstacles.Count}");
                }
                Log($"[Route] RUN#{runId} EDGE#{idx} END totalMs={eMs}");
            }

            var totalMs = Ms(swAll);
            Log($"[Route] RUN#{runId} END totalMs={totalMs} slowEdges={slowEdges}/{edgesOrdered.Count}");

            if (totalMs >= SlowTotalMsThreshold)
                Log($"[Route] RUN#{runId} SLOW totalMs={totalMs} (threshold={SlowTotalMsThreshold})");
        }

        // ---------------- containers ----------------

        private static void TrimEndpointsInsideRects(List<WpfPoint> pts, WpfRect srcRect, WpfRect trgRect)
        {
            if (pts == null || pts.Count == 0) return;

            // NOTE: Máš trim logiku zakomentovanú.
            // Odporúčanie: zatiaľ len diagnostika – aby si videl, či prvý/posledný bod padá do rectu.
            // (Keď budeš chcieť, odkomentujeme while a dáme guard aby nevznikol pts.Count<2).
        }

        private static void StampUsageDense(
            List<WpfPoint> pts,
            WpfRect world,
            double cell,
            Dictionary<(int x, int y), int> usedCells,
            Dictionary<(int x1, int y1, int x2, int y2), int> usedEdges)
        {
            if (pts == null || pts.Count < 2) return;

            int ToGX(double x) => (int)Math.Floor((x - world.Left) / cell);
            int ToGY(double y) => (int)Math.Floor((y - world.Top) / cell);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                int x0 = ToGX(pts[i].X);
                int y0 = ToGY(pts[i].Y);
                int x1 = ToGX(pts[i + 1].X);
                int y1 = ToGY(pts[i + 1].Y);

                if (x0 != x1 && y0 != y1)
                {
                    StampLineBresenham(x0, y0, x1, y1, usedCells, usedEdges);
                    continue;
                }

                int dx = Math.Sign(x1 - x0);
                int dy = Math.Sign(y1 - y0);

                int cx = x0, cy = y0;
                usedCells[(cx, cy)] = usedCells.TryGetValue((cx, cy), out var cc0) ? cc0 + 1 : 1;

                while (cx != x1 || cy != y1)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;

                    usedCells[(nx, ny)] = usedCells.TryGetValue((nx, ny), out var cc) ? cc + 1 : 1;

                    var eKey = NormalizeEdgeKey(cx, cy, nx, ny);
                    usedEdges[eKey] = usedEdges.TryGetValue(eKey, out var ec) ? ec + 1 : 1;

                    cx = nx; cy = ny;
                }
            }
        }

        private static void StampLineBresenham(
                int x0, int y0, int x1, int y1,
                Dictionary<(int x, int y), int> usedCells,
                Dictionary<(int x1, int y1, int x2, int y2), int> usedEdges)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;

            int err = dx - dy;

            int cx = x0, cy = y0;
            usedCells[(cx, cy)] = usedCells.TryGetValue((cx, cy), out var c0) ? c0 + 1 : 1;

            while (!(cx == x1 && cy == y1))
            {
                int px = cx, py = cy;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; cx += sx; }
                if (e2 < dx) { err += dx; cy += sy; }

                usedCells[(cx, cy)] = usedCells.TryGetValue((cx, cy), out var c1) ? c1 + 1 : 1;

                var eKey = NormalizeEdgeKey(px, py, cx, cy);
                usedEdges[eKey] = usedEdges.TryGetValue(eKey, out var ec) ? ec + 1 : 1;
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
            double w = vc.ActualWidth > 0 ? vc.ActualWidth : vc.Width > 0 ? vc.Width : 80;
            double h = vc.ActualHeight > 0 ? vc.ActualHeight : vc.Height > 0 ? vc.Height : 40;
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

            return new WpfRect(minX - pad, minY - pad, maxX - minX + 2 * pad, maxY - minY + 2 * pad);
        }

        private static WpfPoint PickPort(WpfRect from, WpfRect to)
        {
            var fromC = new WpfPoint(from.Left + from.Width / 2, from.Top + from.Height / 2);
            var toC = new WpfPoint(to.Left + to.Width / 2, to.Top + to.Height / 2);

            double dx = toC.X - fromC.X;
            double dy = toC.Y - fromC.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
                return dx >= 0 ? new WpfPoint(from.Right, fromC.Y) : new WpfPoint(from.Left, fromC.Y);
            else
                return dy >= 0 ? new WpfPoint(fromC.X, from.Bottom) : new WpfPoint(fromC.X, from.Top);
        }

        // ---------------- routing (A* + congestion) ----------------

        private sealed record Node(int X, int Y, int Dir);

        private List<WpfPoint>? RouteAStarWithCongestion(
            WpfPoint start,
            WpfPoint goal,
            List<WpfRect> obstacles,
            WpfRect world,
            double cell,
            Dictionary<(int x, int y), int> usedCells,
            Dictionary<(int x1, int y1, int x2, int y2), int> usedEdges,
            double usedCellPenalty,
            double usedEdgePenalty,
            int runId,
            int edgeIdx,
            string srcFull,
            string trgFull)
        {
            var sw = Stopwatch.StartNew();

            int W = (int)Math.Ceiling(world.Width / cell);
            int H = (int)Math.Ceiling(world.Height / cell);
            if (W <= 2 || H <= 2) return null;

            // !!! POZOR: u teba je tu ešte Round() – nechávam, ale logujem to.
            // Ak chceš, prepneme na Floor + center-of-cell všade konzistentne.
            int ToGX(double x) => (int)Math.Round((x - world.Left) / cell);
            int ToGY(double y) => (int)Math.Round((y - world.Top) / cell);
            WpfPoint FromG(int gx, int gy) => new(world.Left + (gx + 0.5) * cell, world.Top + (gy + 0.5) * cell);

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

            int expanded = 0;
            int blockedChecks = 0;
            int segChecks = 0;
            int neighborAttempts = 0;

            while (open.Count > 0)
            {
                var cur = open.Dequeue();
                expanded++;

                if (expanded > AStarMaxExpansions)
                {
                    Log($"[A*] RUN#{runId} EDGE#{edgeIdx} ABORT maxExp={AStarMaxExpansions} src={srcFull} trg={trgFull} W={W} H={H} obs={obstacles.Count} ms={Ms(sw)}");
                    return null;
                }

                if (cur.X == gx2 && cur.Y == gy2)
                {
                    var path = ReconstructPoints(cur, cameFrom, FromG);
                    var ms = Ms(sw);
                    if (expanded >= AStarExpandLogThreshold || ms >= 20)
                    {
                        Log($"[A*] RUN#{runId} EDGE#{edgeIdx} OK exp={expanded} open={open.Count} blocked={blockedChecks} seg={segChecks} neigh={neighborAttempts} ms={ms} pts={path.Count} src={srcFull} trg={trgFull}");
                    }
                    return path;
                }

                var curG = gScore[cur];

                for (int i = 0; i < 4; i++)
                {
                    neighborAttempts++;
                    int nx = cur.X + dxs[i];
                    int ny = cur.Y + dys[i];
                    int nd = dirs[i];

                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;

                    blockedChecks++;
                    if (Blocked(nx, ny)) continue;

                    var pCur = FromG(cur.X, cur.Y);
                    var pNext = FromG(nx, ny);

                    segChecks++;
                    if (SegmentHitsAnyRect(pCur, pNext, obstacles)) continue;

                    double turnPenalty = cur.Dir != 0 && cur.Dir != nd ? 0.7 : 0.0;

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

            Log($"[A*] RUN#{runId} EDGE#{edgeIdx} FAIL exp={expanded} open=0 blocked={blockedChecks} seg={segChecks} neigh={neighborAttempts} ms={Ms(sw)} src={srcFull} trg={trgFull}");
            return null;
        }

        private static (int x1, int y1, int x2, int y2) NormalizeEdgeKey(int x1, int y1, int x2, int y2)
        {
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

                usedCells[(nx, ny)] = usedCells.TryGetValue((nx, ny), out var cc) ? cc + 1 : 1;

                var eKey = NormalizeEdgeKey(px, py, nx, ny);
                usedEdges[eKey] = usedEdges.TryGetValue(eKey, out var ec) ? ec + 1 : 1;

                px = nx; py = ny;
            }
        }

        private static List<WpfPoint> ReconstructPoints(
            Node cur,
            Dictionary<Node, Node> cameFrom,
            Func<int, int, WpfPoint> fromG)
        {
            var path = new List<Node> { cur };
            while (cameFrom.TryGetValue(cur, out var prev))
            {
                cur = prev;
                path.Add(cur);
            }
            path.Reverse();

            var pts = new List<WpfPoint>(path.Count);
            foreach (var n in path)
                pts.Add(fromG(n.X, n.Y));

            return pts;
        }

        private static List<WpfPoint> RemoveNearDuplicates(List<WpfPoint> pts, double eps)
        {
            if (pts.Count < 2) return pts;
            var outPts = new List<WpfPoint>(pts.Count) { pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                var a = outPts[^1];
                var b = pts[i];
                if (Math.Abs(a.X - b.X) <= eps && Math.Abs(a.Y - b.Y) <= eps) continue;
                outPts.Add(b);
            }
            return outPts;
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

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;

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
                    Math.Abs(a.X - b.X) < 0.01 && Math.Abs(b.X - c.X) < 0.01 ||
                    Math.Abs(a.Y - b.Y) < 0.01 && Math.Abs(b.Y - c.Y) < 0.01;

                if (!collinear) outPts.Add(b);
            }

            outPts.Add(pts[^1]);
            return outPts;
        }

        private static bool SegmentHitsAnyRect(WpfPoint a, WpfPoint b, List<WpfRect> obstacles)
        {
            foreach (var r in obstacles)
            {
                if (r.Contains(a) || r.Contains(b)) return true;
                if (LineIntersectsRect(a, b, r)) return true;
            }
            return false;
        }

        private static WpfPoint PushOutFromRect(WpfRect from, WpfPoint port, WpfRect toward, double lead)
        {
            var fromC = new WpfPoint(from.Left + from.Width / 2, from.Top + from.Height / 2);
            var toC = new WpfPoint(toward.Left + toward.Width / 2, toward.Top + toward.Height / 2);

            double dx = toC.X - fromC.X;
            double dy = toC.Y - fromC.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
                return new WpfPoint(port.X + (dx >= 0 ? lead : -lead), port.Y);
            else
                return new WpfPoint(port.X, port.Y + (dy >= 0 ? lead : -lead));
        }

        private static double PolylineLength(List<WpfPoint> pts)
        {
            double len = 0;
            for (int i = 0; i < pts.Count - 1; i++)
                len += (pts[i + 1] - pts[i]).Length;
            return len;
        }

        private static List<WpfPoint> ForceDoglegIfTooShort(
            List<WpfPoint> pts, List<WpfRect> obstacles, double offset)
        {
            if (pts.Count < 2) return pts;

            var a = pts[0];
            var b = pts[^1];

            var midX = a.X + offset;
            var p1 = new WpfPoint(midX, a.Y);
            var p2 = new WpfPoint(midX, b.Y);

            if (SegmentHitsAnyRect(a, p1, obstacles) || SegmentHitsAnyRect(p1, p2, obstacles) || SegmentHitsAnyRect(p2, b, obstacles))
            {
                midX = a.X - offset;
                p1 = new WpfPoint(midX, a.Y);
                p2 = new WpfPoint(midX, b.Y);
                if (SegmentHitsAnyRect(a, p1, obstacles) || SegmentHitsAnyRect(p1, p2, obstacles) || SegmentHitsAnyRect(p2, b, obstacles))
                    return pts;
            }

            return new List<WpfPoint> { a, p1, p2, b };
        }

        private enum Side { Left, Right, Top, Bottom }

        private static Side SideNearestToPoint(WpfRect r, WpfPoint p)
        {
            const double M = 6;

            var leftPt = new WpfPoint(r.Left, ClampD(p.Y, r.Top + M, r.Bottom - M));
            var rightPt = new WpfPoint(r.Right, ClampD(p.Y, r.Top + M, r.Bottom - M));
            var topPt = new WpfPoint(ClampD(p.X, r.Left + M, r.Right - M), r.Top);
            var bottomPt = new WpfPoint(ClampD(p.X, r.Left + M, r.Right - M), r.Bottom);

            double dL = Dist2(p, leftPt);
            double dR = Dist2(p, rightPt);
            double dT = Dist2(p, topPt);
            double dB = Dist2(p, bottomPt);

            double min = dL;
            Side best = Side.Left;

            if (dR < min) { min = dR; best = Side.Right; }
            if (dT < min) { min = dT; best = Side.Top; }
            if (dB < min) { min = dB; best = Side.Bottom; }

            return best;
        }

        private static WpfPoint ClosestPointOnSide(WpfRect r, Side s, WpfPoint p)
        {
            const double M = 6;

            return s switch
            {
                Side.Left => new WpfPoint(r.Left, ClampD(p.Y, r.Top + M, r.Bottom - M)),
                Side.Right => new WpfPoint(r.Right, ClampD(p.Y, r.Top + M, r.Bottom - M)),
                Side.Top => new WpfPoint(ClampD(p.X, r.Left + M, r.Right - M), r.Top),
                Side.Bottom => new WpfPoint(ClampD(p.X, r.Left + M, r.Right - M), r.Bottom),
                _ => new WpfPoint(r.Right, r.Top)
            };
        }

        private static double Dist2(WpfPoint a, WpfPoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static WpfPoint OffsetOut(WpfPoint p, Side s, double d) => s switch
        {
            Side.Right => new WpfPoint(p.X + d, p.Y),
            Side.Left => new WpfPoint(p.X - d, p.Y),
            Side.Bottom => new WpfPoint(p.X, p.Y + d),
            Side.Top => new WpfPoint(p.X, p.Y - d),
            _ => p
        };

        private static double ClampD(double v, double min, double max) => v < min ? min : v > max ? max : v;

        private static void ApplyDirectedPorts(
            List<WpfPoint> pts,
            WpfRect srcRect,
            WpfRect trgRect,
            double portOut,
            double leadOut)
        {
            if (pts == null || pts.Count < 2) return;

            var startApproach = pts.Count >= 2 ? pts[1] : pts[0];
            var endApproach = pts.Count >= 2 ? pts[^2] : pts[^1];

            var sSide = SideNearestToPoint(srcRect, startApproach);
            var sPort = ClosestPointOnSide(srcRect, sSide, startApproach);
            var sOut = OffsetOut(sPort, sSide, portOut);
            var sLead = OffsetOut(sPort, sSide, portOut + leadOut);

            pts[0] = sLead;

            if (pts.Count >= 2)
                InsertGuideAfterLead(pts, 0, sSide);

            pts.Insert(0, sOut);
            pts.Insert(0, sPort);

            var tSide = SideNearestToPoint(trgRect, endApproach);
            var tPort = ClosestPointOnSide(trgRect, tSide, endApproach);
            var tOut = OffsetOut(tPort, tSide, portOut);
            var tLead = OffsetOut(tPort, tSide, portOut + leadOut);

            pts[^1] = tLead;

            if (pts.Count >= 2)
                InsertGuideBeforeLead(pts, pts.Count - 1, tSide);

            pts.Add(tOut);
            pts.Add(tPort);

            var cleaned = SimplifyCollinear(RemoveNearDuplicates(pts, 0.5));
            pts.Clear();
            pts.AddRange(cleaned);
        }

        private static void InsertGuideAfterLead(List<WpfPoint> pts, int leadIndex, Side side)
        {
            if (leadIndex < 0 || leadIndex >= pts.Count - 1) return;

            var lead = pts[leadIndex];
            var next = pts[leadIndex + 1];

            if (side == Side.Left || side == Side.Right)
            {
                if (Math.Abs(next.Y - lead.Y) < 0.01) return;
                var guide = new WpfPoint(next.X, lead.Y);
                pts.Insert(leadIndex + 1, guide);
            }
            else
            {
                if (Math.Abs(next.X - lead.X) < 0.01) return;
                var guide = new WpfPoint(lead.X, next.Y);
                pts.Insert(leadIndex + 1, guide);
            }
        }

        private static void InsertGuideBeforeLead(List<WpfPoint> pts, int leadIndex, Side side)
        {
            if (leadIndex <= 0 || leadIndex >= pts.Count) return;

            var prev = pts[leadIndex - 1];
            var lead = pts[leadIndex];

            if (side == Side.Left || side == Side.Right)
            {
                if (Math.Abs(prev.Y - lead.Y) < 0.01) return;
                var guide = new WpfPoint(prev.X, lead.Y);
                pts.Insert(leadIndex, guide);
            }
            else
            {
                if (Math.Abs(prev.X - lead.X) < 0.01) return;
                var guide = new WpfPoint(lead.X, prev.Y);
                pts.Insert(leadIndex, guide);
            }
        }

        // --- tvoje SegmentIntersectsRect / SegmentsIntersect nechávam nezmenené ---
        static bool SegmentIntersectsRect(WpfPoint a, WpfPoint b, WpfRect r)
        {
            if (r.Contains(a) || r.Contains(b)) return true;

            var segBB = new WpfRect(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X),
                Math.Abs(a.Y - b.Y));
            if (!r.IntersectsWith(segBB)) return false;

            var p1 = new WpfPoint(r.Left, r.Top);
            var p2 = new WpfPoint(r.Right, r.Top);
            var p3 = new WpfPoint(r.Right, r.Bottom);
            var p4 = new WpfPoint(r.Left, r.Bottom);

            return SegmentsIntersect(a, b, p1, p2) ||
                   SegmentsIntersect(a, b, p2, p3) ||
                   SegmentsIntersect(a, b, p3, p4) ||
                   SegmentsIntersect(a, b, p4, p1);
        }

        static bool SegmentsIntersect(WpfPoint a, WpfPoint b, WpfPoint c, WpfPoint d)
        {
            static double Orient(WpfPoint p, WpfPoint q, WpfPoint r)
                => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

            static bool OnSeg(WpfPoint p, WpfPoint q, WpfPoint r)
                => Math.Min(p.X, q.X) - 1e-9 <= r.X && r.X <= Math.Max(p.X, q.X) + 1e-9 &&
                   Math.Min(p.Y, q.Y) - 1e-9 <= r.Y && r.Y <= Math.Max(p.Y, q.Y) + 1e-9;

            var o1 = Orient(a, b, c);
            var o2 = Orient(a, b, d);
            var o3 = Orient(c, d, a);
            var o4 = Orient(c, d, b);

            if ((o1 > 0 && o2 < 0 || o1 < 0 && o2 > 0) &&
                (o3 > 0 && o4 < 0 || o3 < 0 && o4 > 0))
                return true;

            if (Math.Abs(o1) < 1e-9 && OnSeg(a, b, c)) return true;
            if (Math.Abs(o2) < 1e-9 && OnSeg(a, b, d)) return true;
            if (Math.Abs(o3) < 1e-9 && OnSeg(c, d, a)) return true;
            if (Math.Abs(o4) < 1e-9 && OnSeg(c, d, b)) return true;

            return false;
        }
    }
}
