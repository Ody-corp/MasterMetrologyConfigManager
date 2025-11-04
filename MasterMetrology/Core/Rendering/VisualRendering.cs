using GraphX;
using GraphX.Controls;
using GraphX.PCL.Common.Enums;
using MasterMetrology.Core.GraphX;
using MasterMetrology.Core.GraphX.Controls;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MasterMetrology.Core.Rendering
{
    internal class VisualRendering
    {
        public StateGraphArea LastGraphArea { get; private set; }
        public StateGraph LastGraph { get; private set; }

        public void RenderGraph(List<StateModelData> states, Canvas graphLayer, Action<GraphVertex> onVertexSelected = null)
        {
            graphLayer.Children.Clear();

            // 1️⃣ Vytvoríme GraphX graf
            var graph = new StateGraph();
            LastGraph = graph;

            // Mapovanie state -> vertex
            var vertexMap = new Dictionary<string, GraphVertex>();

            // 2️⃣ Najprv pridaj všetky stavy ako uzly
            foreach (var state in states)
                AddStateRecursive(state, graph, vertexMap);

            // 3️⃣ Potom pridaj všetky prechody ako hrany
            foreach (var state in states)
                AddTransitionsRecursive(state, graph, vertexMap);


            // 4️⃣ Nastav GraphX logic
            var logicCore = new StateLogicCore
            {
                Graph = graph,
                DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.FR,
                DefaultOverlapRemovalAlgorithm = OverlapRemovalAlgorithmTypeEnum.OneWayFSA,
                DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.None,
                EnableParallelEdges = true 
            };

            var factory = new CustomControlFactory();
            

            // 5️⃣ Vytvor GraphArea a vygeneruj vizuál
            var graphArea = new StateGraphArea
            {
                LogicCore = logicCore,
                Width = Config.DEFAULT_VALUE_CANVAS_X,
                Height = Config.DEFAULT_VALUE_CANVAS_Y,
                Background = Brushes.Transparent,
                ControlFactory = factory
            };

            factory.FactoryRootArea = graphArea;

            LastGraphArea = graphArea;

            graphLayer.Children.Add(graphArea);

            graphArea.GenerateGraph(true);
            graphArea.SetVerticesDrag(true, true);
            graphArea.ShowAllEdgesLabels(true);

            foreach (var vc in graphArea.VertexList.Values)
            {
                // capture local
                var vertexObj = vc.Vertex as GraphVertex;
                if (vertexObj == null) continue;

                vc.MouseLeftButtonUp += (s, e) =>
                {
                    try
                    {
                        onVertexSelected?.Invoke(vertexObj);
                        e.Handled = true;
                    }
                    catch { }
                };
            }

            UpdateSubPositions(graphArea, graph);

            graphArea.LayoutUpdated += (s, e) => UpdateSubPositions(graphArea, graph);


            graphArea.RelayoutGraph();

            // Zarovnaj do stredu
            double centerX = (Config.DEFAULT_VALUE_CANVAS_X - graphArea.DesiredSize.Width) / 2;
            double centerY = (Config.DEFAULT_VALUE_CANVAS_Y - graphArea.DesiredSize.Height) / 2;
            Canvas.SetLeft(graphArea, centerX);
            Canvas.SetTop(graphArea, centerY);
        }

        // Metóda na refresh vizuálu jedného vertexu (napr. po Apply)
        public void RefreshVertexVisual(GraphVertex vertex)
        {
            if (LastGraphArea == null || vertex == null) return;
            if (!LastGraphArea.VertexList.TryGetValue(vertex, out var vc)) return;

            // ak máš SimpleVertexControl / SectionVertexControl, updateni ich natívnym API
            if (vc is SimpleVertexControl simple)
            {
                simple.UpdateLabel(vertex.State?.Name ?? "");
            }
            else if (vc is SectionVertexControl section)
            {
                section.SetTitle(vertex.State?.Name ?? "");
                // prípadne update size ak potrebuješ:
                // section.SetSize(newWidth, newHeight);
            }
        }

        // Helper: nastav pozície sub-vertexov podľa pozície sekcie
        private void UpdateSubPositions(StateGraphArea graphArea, StateGraph graph)
        {
            if (graphArea?.VertexList == null) return;

            foreach (var section in graph.Vertices.OfType<GraphVertexSection>())
            {
                if (!graphArea.VertexList.TryGetValue(section, out var vcSection)) continue;

                var sectionPos = vcSection.GetPosition();
                var secControl = vcSection as SectionVertexControl;
                if (secControl == null) continue;

                // definuj vnútorné paddingy a offsets
                double offsetX = 12;
                double offsetYStart = 44;
                double spacing = 60;

                double maxRight = 0;
                double maxBottom = offsetYStart;

                for (int i = 0; i < section.SubVertices.Count; i++)
                {
                    var sub = section.SubVertices[i];
                    if (!graphArea.VertexList.TryGetValue(sub, out var vcSub)) continue;

                    double relX = offsetX;
                    double relY = offsetYStart + i * spacing;
                    double finalX = sectionPos.X + relX;
                    double finalY = sectionPos.Y + relY;

                    // Z-index: sub nad sekciou
                    if (vcSub is UIElement subUi) Panel.SetZIndex(subUi, 1);
                    if (vcSection is UIElement secUi) Panel.SetZIndex(secUi, 0);

                    // nastav poziciu sub-vertexu
                    try
                    {
                        vcSub.SetPosition(finalX, finalY);
                    }
                    catch
                    {
                        Canvas.SetLeft(vcSub, finalX);
                        Canvas.SetTop(vcSub, finalY);
                    }

                    // track boundary for auto-size
                    double right = relX + (vcSub?.ActualWidth ?? vcSub?.Width ?? 120);
                    double bottom = relY + (vcSub?.ActualHeight ?? vcSub?.Height ?? 48);
                    if (right > maxRight) maxRight = right;
                    if (bottom > maxBottom) maxBottom = bottom;
                }

                // compute new size with padding
                double newWidth = Math.Max(secControl.SectionWidth, maxRight + 20);
                double newHeight = Math.Max(secControl.SectionHeight, maxBottom + 20);

                // apply size if changed
                if (Math.Abs(newWidth - secControl.SectionWidth) > 1 || Math.Abs(newHeight - secControl.SectionHeight) > 1)
                {
                    secControl.SetSize(newWidth, newHeight);
                }
            }
        }

        private GraphVertex AddStateRecursive(StateModelData state, StateGraph graph, Dictionary<string, GraphVertex> map)
        {
            if (state == null) return null;

            GraphVertex vertex;

            if (state.SubStatesData != null && state.SubStatesData.Count > 0)
            {
                var sectionVertex = new GraphVertexSection(state);
                vertex = sectionVertex;

                foreach (var sub in state.SubStatesData)
                {
                    var child = AddStateRecursive(sub, graph, map);
                    if (child != null)
                        sectionVertex.SubVertices.Add(child);
                }
            }
            else
            {
                vertex = new GraphVertex(state);
            }

            graph.AddVertex(vertex);
            map[state.FullIndex] = vertex;
            return vertex;
        }



        private void AddTransitionsRecursive(StateModelData state, StateGraph graph, Dictionary<string, GraphVertex> map)
        {
            if (state.TransitionsData == null || state.TransitionsData.Count == 0)
            {
                foreach (var sub in state.SubStatesData)
                {
                    AddTransitionsRecursive(sub, graph, map);
                }  
            }
            else
            {
                // Zoskup podľa cieľa
                var grouped = state.TransitionsData.GroupBy(t => t.NextStage);
                foreach (var g in grouped)
                {
                    if (!map.TryGetValue(state.FullIndex, out var from))
                    {
                        Debug.WriteLine($"⚠️ Missing FROM: {state.FullIndex}");
                        continue;
                    }

                    if (!map.TryGetValue(g.Key, out var to))
                    {
                        Debug.WriteLine($"⚠️ Missing TO: {g.Key} (from {state.FullIndex})");
                        continue;
                    }

                    var rep = g.First();
                    var edge = new GraphEdge(from, to, rep)
                    {
                        Label = string.Join(", ", g.Select(t => t.Input).Where(s => !string.IsNullOrWhiteSpace(s)))
                    };

                    graph.AddEdge(edge);
                }
            }
        }

        public void RemoveTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            // nájdi GraphEdge (model) a EdgeControl (vizuál)
            var ec = LastGraphArea.EdgesList.Values.FirstOrDefault(e => (e.Edge as GraphEdge)?.Transition == transition);
            var modelEdge = ec?.Edge as GraphEdge;

            if (modelEdge != null)
            {
                // odstráň model hrany
                LastGraph.RemoveEdge(modelEdge);

                // odstráň vizuál (EdgeControl)
                try
                {
                    LastGraphArea.RemoveEdge(modelEdge);
                }
                catch { }

                // refresh / relayout
                try { LastGraphArea.RelayoutGraph(); } catch { LastGraphArea.UpdateLayout(); }
            }
        }

        public void AddTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            // nájdi zodpovedajúce GraphVertex (podľa FullIndex)
            var from = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.FromStage);
            var to = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.NextStage);

            if (from == null || to == null)
            {
                // ak nie je vertex v grafe, nemôžeme pridať (môžeš rozšíriť, aby sme pridali aj nové vertexy)
                return;
            }

            var newEdge = new GraphEdge(from, to, transition)
            {
                Label = transition.Input ?? ""
            };

            LastGraph.AddEdge(newEdge);

            // re-generate / relayout graf tak, aby sa zobrazil nová hrana
            try
            {
                LastGraphArea.GenerateGraph(true);
                LastGraphArea.RelayoutGraph();
            }
            catch
            {
                try { LastGraphArea.RelayoutGraph(); } catch { LastGraphArea.UpdateLayout(); }
            }
        }
    }
}
