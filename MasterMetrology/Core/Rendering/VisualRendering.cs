using GraphX;
using GraphX.Controls;
using GraphX.PCL.Common.Enums;
using MasterMetrology.Core.GraphX;
using MasterMetrology.Core.GraphX.Controls;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using QuickGraph;
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

                    var transitionsForThisPair = g.ToList();
                    var edge = new GraphEdge(from, to, transitionsForThisPair);

                    graph.AddEdge(edge);
                }
            }
        }

        public void RemoveTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            // nájdi GraphEdge (model) a EdgeControl (vizuál)
            var modelEdge = LastGraph.Edges.OfType<GraphEdge>().FirstOrDefault(e => e.Transitions.Contains(transition));
            
            bool removed = modelEdge.RemoveTransition(transition);
            if (!removed) return;
            Debug.WriteLine("1. CHECKPOINT");
            LastGraphArea.Dispatcher.Invoke(() =>
            {
                Debug.WriteLine("2. CHECKPOINT");
                if (modelEdge.Transitions.Count > 0)
                {
                    Debug.WriteLine("3.1. CHECKPOINT");
                    // update edge control label
                    if (LastGraphArea.EdgesList.TryGetValue(modelEdge, out var ec))
                    {
                        /*Debug.WriteLine("4. CHECKPOINT");
                        try { 
                            ec.UpdateEdge(true);
                            Debug.WriteLine("5. CHECKPOINT");
                        } catch { }*/
                        /*try
                        {
                            ec.InvalidateMeasure();
                            ec.InvalidateVisual();
                            LastGraphArea.UpdateLayout();
                            Debug.WriteLine("5. CHECKPOINT - invalidated & UpdateLayout");
                        }
                        catch { }*/
                        modelEdge.UpdateLabelFromTransitions();
                        //UpdateEdgeLabelVisual(ec, modelEdge.Label);
                    }
                }
                else
                {
                    Debug.WriteLine("3.2. CHECKPOINT");
                    // no more transitions -> remove edge from model and view
                    LastGraph.RemoveEdge(modelEdge);
                    LastGraphArea.RemoveEdge(modelEdge);
                }
            }, DispatcherPriority.Normal);
        }

        public void AddTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;
            
            var from = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.FromStage);
            var to = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.NextStage);

            var existingEdge = LastGraph.Edges.FirstOrDefault(e => e.Source == from && e.Target == to) as GraphEdge;
            if (existingEdge != null)
            {
                // aktualizovať EdgeControl label ak existuje:
                existingEdge.AddTransition(transition);
                LastGraphArea.Dispatcher.Invoke(() =>
                {
                    if (LastGraphArea.EdgesList.TryGetValue(existingEdge, out var ec))
                    {
                        try { ec.UpdateEdge(true); } catch { }
                    }
                }, DispatcherPriority.Normal);
                // alebo ec.UpdateEdgeRendering(...) podľa API
            }
            else
            {
                // ak hrana neexistuje -> pridaj model edge a vytvor vizuál bez posunu vertexov
                // 1) uloz pozicie vertexov
                var positions = LastGraphArea.VertexList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetPosition());

                // 2) pridaj modelovu hranu
                /*var newEdge = new GraphEdge(from, to, new[] { transition });
                LastGraph.AddEdge(newEdge);

                // 3) regenerate/refresh edges, ale zachovame pozicie vertexy
                LastGraphArea.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // zjednodušené: vygenerujeme edge-controls (bez presunu vertexov, preto ich zarovno obnovíme)
                        LastGraphArea.GenerateGraph(true);
                    }
                    catch
                    {
                        try { LastGraphArea.RelayoutGraph(); } catch { }
                    }

                    // obnov pozicie vertexov aby nebol viditelny presun
                    foreach (var kv in positions)
                    {
                        if (LastGraphArea.VertexList.TryGetValue(kv.Key, out var vc))
                        {
                            try { vc.SetPosition(kv.Value); } catch { Canvas.SetLeft(vc, kv.Value.X); Canvas.SetTop(vc, kv.Value.Y); }
                        }
                    }

                    // update edge label (najmä novovytvorena)
                    if (LastGraphArea.EdgesList.TryGetValue(newEdge, out var ecNew))
                    {
                        try { ecNew.UpdateEdge(true); } catch { }
                    }
                }, DispatcherPriority.Background);*/

                var newEdge = new GraphEdge(from, to, new[] { transition });
                LastGraph.AddEdge(newEdge);

                // 3) generate only edges for 'from' vertex (or both) - avoid full relayout
                LastGraphArea.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Preferred: generate edges only for the 'from' vertex (less invasive)
                        // NOTE: GenerateEdgesForVertex may exist in your GraphX build.
                        var method = LastGraphArea.GetType().GetMethod("GenerateEdgesForVertex", new Type[] { from.GetType() });
                        if (method != null)
                        {
                            // call GenerateEdgesForVertex(from)
                            method.Invoke(LastGraphArea, new object[] { from });
                        }
                        else
                        {
                            // Fallback: generate all edges (less ideal)
                            var genAll = LastGraphArea.GetType().GetMethod("GenerateAllEdges", new Type[] { });
                            if (genAll != null) genAll.Invoke(LastGraphArea, null);
                            else
                            {
                                // Ultimate fallback: try to create edge-control via ControlFactory (best-effort)
                                try
                                {
                                    var factory = LastGraphArea.ControlFactory;
                                    var createEdge = factory?.GetType().GetMethod("CreateEdgeControl");
                                    if (createEdge != null)
                                    {
                                        var ec = createEdge.Invoke(factory, new object[] { newEdge }) as EdgeControl;
                                        if (ec != null)
                                        {
                                            // register it in area
                                            var addEdgeMethod = LastGraphArea.GetType().GetMethod("AddEdge", new Type[] { newEdge.GetType(), ec.GetType() });
                                            if (addEdgeMethod != null) addEdgeMethod.Invoke(LastGraphArea, new object[] { newEdge, ec });
                                        }
                                    }
                                }
                                catch { /* best-effort fallback */ }
                            }
                        }
                    }
                    catch
                    {
                        // Last resort: call GenerateGraph (will relayout) – but we try to avoid this.
                        try { LastGraphArea.GenerateGraph(true); } catch { }
                    }

                    // 4) restore saved vertex positions to avoid perceived movement
                    foreach (var kv in positions)
                    {
                        if (LastGraphArea.VertexList.TryGetValue(kv.Key, out var vc))
                        {
                            try { vc.SetPosition(kv.Value); } catch { Canvas.SetLeft(vc, kv.Value.X); Canvas.SetTop(vc, kv.Value.Y); }
                        }
                    }

                    // 5) ensure newly created edge label is updated
                    if (LastGraphArea.EdgesList.TryGetValue(newEdge, out var newEc))
                    {
                        try { newEc.UpdateEdge(true); } catch { }
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        

    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void UpdateEdgeLabelVisual(EdgeControl ec, string newLabel)
        {
            if (ec == null) return;
            try
            {
                // Najprv skús nájsť TextBlock vnútri EdgeControl
                var tb = FindVisualChild<TextBlock>(ec);
                if (tb != null)
                {
                    tb.Text = newLabel ?? string.Empty;
                    tb.InvalidateVisual();
                }
                // Force refresh
                ec.InvalidateMeasure();
                ec.InvalidateVisual();
                if (ec.Parent is UIElement p) p.UpdateLayout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UpdateEdgeLabelVisual failed: " + ex.Message);
            }
        }

    }
}
