using GraphX;
using GraphX.Controls;
using GraphX.PCL.Common;
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
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MasterMetrology.Core.Rendering
{
    internal class VisualRendering
    {
        public StateGraphArea LastGraphArea { get; private set; }
        public StateGraph LastGraph { get; private set; }

        // mapovanie modelEdge -> attachable label control (aby sme ich vedeli odpojiť pri mazani)
        private readonly Dictionary<GraphEdge, AttachableEdgeLabelControl> _edgeLabelMap = new Dictionary<GraphEdge, AttachableEdgeLabelControl>();

        public void RenderGraph(List<StateModelData> states, Canvas graphLayer, Action<GraphVertex> onVertexSelected = null)
        {
            // detach any previously attached labels (safety)
            try
            {
                foreach (var kv in _edgeLabelMap.ToList())
                    DetachLabelForEdge(kv.Key);
                _edgeLabelMap.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RenderGraph: failed to detach old labels: " + ex.Message);
            }

            graphLayer.Children.Clear();

            var graph = new StateGraph();
            LastGraph = graph;

            var vertexMap = new Dictionary<string, GraphVertex>();

            foreach (var state in states)
                AddStateRecursive(state, graph, vertexMap);

            foreach (var state in states)
                AddTransitionsRecursive(state, graph, vertexMap);

            var logicCore = new StateLogicCore
            {
                Graph = graph,
                DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.FR,
                DefaultOverlapRemovalAlgorithm = OverlapRemovalAlgorithmTypeEnum.OneWayFSA,
                DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.None,
                EnableParallelEdges = true
            };

            var factory = new CustomControlFactory();

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

            // generate graph (do NOT call ShowAllEdgesLabels here - we manage labels ourselves)
            graphArea.GenerateGraph(true);
            graphArea.SetVerticesDrag(true, true);
            // graphArea.ShowAllEdgesLabels(true); // <-- removed to avoid attachable label timing issues

            // Attach labels for all generated edges (after GenerateGraph)
            try
            {
                foreach (var kv in graphArea.EdgesList)
                {
                    if (kv.Key is GraphEdge modelEdge && kv.Value is EdgeControl ec)
                        AttachBindableLabelToEdge(ec, modelEdge);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RenderGraph: error attaching labels: " + ex.Message);
            }

            foreach (var vc in graphArea.VertexList.Values)
            {
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

            double centerX = (Config.DEFAULT_VALUE_CANVAS_X - graphArea.DesiredSize.Width) / 2;
            double centerY = (Config.DEFAULT_VALUE_CANVAS_Y - graphArea.DesiredSize.Height) / 2;
            Canvas.SetLeft(graphArea, centerX);
            Canvas.SetTop(graphArea, centerY);
        }

        private void UpdateSubPositions(StateGraphArea graphArea, StateGraph graph)
        {
            if (graphArea?.VertexList == null) return;

            foreach (var section in graph.Vertices.OfType<GraphVertexSection>())
            {
                if (!graphArea.VertexList.TryGetValue(section, out var vcSection)) continue;

                var sectionPos = vcSection.GetPosition();
                var secControl = vcSection as SectionVertexControl;
                if (secControl == null) continue;

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

                    if (vcSub is UIElement subUi) Panel.SetZIndex(subUi, 1);
                    if (vcSection is UIElement secUi) Panel.SetZIndex(secUi, 0);

                    try { vcSub.SetPosition(finalX, finalY); } catch { Canvas.SetLeft(vcSub, finalX); Canvas.SetTop(vcSub, finalY); }

                    double right = relX + (vcSub?.ActualWidth ?? vcSub?.Width ?? 120);
                    double bottom = relY + (vcSub?.ActualHeight ?? vcSub?.Height ?? 48);
                    if (right > maxRight) maxRight = right;
                    if (bottom > maxBottom) maxBottom = bottom;
                }

                double newWidth = Math.Max(secControl.SectionWidth, maxRight + 20);
                double newHeight = Math.Max(secControl.SectionHeight, maxBottom + 20);

                if (Math.Abs(newWidth - secControl.SectionWidth) > 1 || Math.Abs(newHeight - secControl.SectionHeight) > 1)
                    secControl.SetSize(newWidth, newHeight);
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
                    AddTransitionsRecursive(sub, graph, map);
            }
            else
            {
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

        private void AttachBindableLabelToEdge(EdgeControl ec, GraphEdge modelEdge)
        {
            if (ec == null || modelEdge == null || LastGraphArea == null) return;

            try
            {
                // If already exists, detach previous one
                if (_edgeLabelMap.TryGetValue(modelEdge, out var existing) && existing != null)
                {
                    try { existing.ShowLabel = false; } catch { }
                    try { existing.Detach(); } catch { }
                    try { if (LastGraphArea.Children.Contains(existing)) LastGraphArea.Children.Remove(existing); } catch { }
                    _edgeLabelMap.Remove(modelEdge);
                }

                var attachLabel = new AttachableEdgeLabelControl
                {
                    // do NOT set ShowLabel = true yet
                };

                var binding = new Binding(nameof(GraphEdge.Text))
                {
                    Source = modelEdge,
                    Mode = BindingMode.OneWay
                };
                attachLabel.SetBinding(ContentControl.ContentProperty, binding);

                // IMPORTANT: Attach to edge control first (this registers label and places it into RootArea.Children)
                attachLabel.Attach(ec);

                // After it's attached to visual tree we can safely enable ShowLabel
                attachLabel.ShowLabel = true;

                // store mapping for later detach
                _edgeLabelMap[modelEdge] = attachLabel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AttachBindableLabelToEdge failed: " + ex.Message);
            }
        }

        private void DetachLabelForEdge(GraphEdge modelEdge)
        {
            if (modelEdge == null || LastGraphArea == null) return;
            try
            {
                if (_edgeLabelMap.TryGetValue(modelEdge, out var label) && label != null)
                {
                    try { label.ShowLabel = false; } catch { }
                    try { label.Detach(); } catch (Exception ex) { Debug.WriteLine("DetachLabelForEdge: label.Detach() failed: " + ex.Message); }
                    try { if (LastGraphArea.Children.Contains(label)) LastGraphArea.Children.Remove(label); } catch (Exception ex) { Debug.WriteLine("DetachLabelForEdge: remove from RootArea failed: " + ex.Message); }
                    _edgeLabelMap.Remove(modelEdge);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DetachLabelForEdge failed: " + ex.Message);
            }
        }

        public void RemoveTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            var modelEdge = LastGraph.Edges.OfType<GraphEdge>().FirstOrDefault(e => e.Transitions.Contains(transition));
            if (modelEdge == null)
            {
                Debug.WriteLine("RemoveTransition: model edge not found.");
                return;
            }

            var removed = modelEdge.RemoveTransition(transition);
            Debug.WriteLine($"RemoveTransition: removed from model, new label='{modelEdge.Text}', removedFlag={removed}");
            if (!removed) 
            {
                return;
            }

            if (modelEdge.Transitions.Count == 0)
            {
                LastGraph.RemoveEdge(modelEdge);
                Debug.WriteLine("RemoveTransition: removed edge from visual");
            }

            LastGraphArea.GenerateAllEdges();
        }

        public void AddTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            var from = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.FromStage);
            var to = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.NextStage);

            if (from == null || to == null)
            {
                Debug.WriteLine($"AddTransition: source/target vertex not found (from={transition.FromStage}, to={transition.NextStage})");
                return;
            }

            var existingEdge = LastGraph.Edges.FirstOrDefault(e => e.Source == from && e.Target == to) as GraphEdge;
            if (existingEdge != null)
            {
                existingEdge.AddTransition(transition);
                Debug.WriteLine($"AddTransition: added into existing edge, new label='{existingEdge.Text}'");
            }
            else
            {
                var savedPositions = LastGraphArea.VertexList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetPosition());

                var newEdge = new GraphEdge(from, to, new[] { transition });
                LastGraph.AddEdge(newEdge);
            }

            LastGraphArea.GenerateAllEdges();
        }
    }
}
