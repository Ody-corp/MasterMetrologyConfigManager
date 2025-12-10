using GraphX.Controls;
using GraphX.PCL.Common.Enums;
using MasterMetrology.Core.GraphX;
using MasterMetrology.Core.GraphX.Controls;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace MasterMetrology.Core.Rendering
{
    internal class VisualRendering
    {
        public StateGraphArea LastGraphArea { get; private set; }
        public StateGraph LastGraph { get; private set; }

        // mapovanie modelEdge -> attachable label control
        private readonly Dictionary<GraphEdge, AttachableEdgeLabelControl> _edgeLabelMap = new Dictionary<GraphEdge, AttachableEdgeLabelControl>();

        private EventHandler _layoutUpdatedHandler;

        public void RenderGraph(List<StateModelData> states, Canvas graphLayer, Action<GraphVertex> onVertexSelected = null)
        {
            CleanupLastGraphArea(graphLayer);

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

            // generate graph (do NOT call ShowAllEdgesLabels here - manage labels ourselves)
            graphArea.GenerateGraph(true);
            graphArea.SetVerticesDrag(true, true);

            // attach named handler
            _layoutUpdatedHandler = (s, e) => UpdateSubPositions(graphArea, graph);
            graphArea.LayoutUpdated += _layoutUpdatedHandler;

            // Attach labels for generated edges (after GenerateGraph)
            var dispatcher = graphArea.Dispatcher ?? Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
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
            }
            ), DispatcherPriority.Loaded);
            
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

            // center
            double centerX = (Config.DEFAULT_VALUE_CANVAS_X - graphArea.DesiredSize.Width) / 2;
            double centerY = (Config.DEFAULT_VALUE_CANVAS_Y - graphArea.DesiredSize.Height) / 2;
            Canvas.SetLeft(graphArea, centerX);
            Canvas.SetTop(graphArea, centerY);
        }

        /// <summary>
        /// Bezpečný cleanup pred vytváraním nového GraphArea: odoberie layout handler, detachne a odstráni attachable labely,
        /// odstráni starý GraphArea z parentu a vyčistí interné mapy.
        /// </summary>
        private void CleanupLastGraphArea(Canvas graphLayer)
        {
            var old = LastGraphArea;
            if (old == null) return;

            try
            {
                if (_layoutUpdatedHandler != null)
                {
                    try { old.LayoutUpdated -= _layoutUpdatedHandler; } catch { }
                    _layoutUpdatedHandler = null;
                }

                foreach (var kv in _edgeLabelMap.ToList())
                {
                    var label = kv.Value;
                    try { if (label != null) label.ShowLabel = false; } catch { }
                    try { if (label != null) label.Detach(); } catch (Exception ex) { Debug.WriteLine("Cleanup detach failed: " + ex.Message); }
                    try { if (label != null && old.Children.Contains(label)) old.Children.Remove(label); } catch { }
                }
                _edgeLabelMap.Clear();

                try
                {
                    if (old.Parent is Panel parentPanel)
                    {
                        parentPanel.Children.Remove(old);
                    }
                    else if (graphLayer != null && graphLayer.Children.Contains(old))
                    {
                        graphLayer.Children.Remove(old);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Cleanup remove old GraphArea failed: " + ex.Message);
                }

                LastGraphArea = null;
                LastGraph = null;

                try
                {
                    var disp = old.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    disp.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CleanupLastGraphArea failed: " + ex.Message);
            }
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
                var grouped = state.TransitionsData.GroupBy(t => t.NextState.FullIndex);
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
                    // don't set ShowLabel here — set it after attach + dispatcher
                };

                var binding = new Binding(nameof(GraphEdge.Text))
                {
                    Source = modelEdge,
                    Mode = BindingMode.OneWay
                };
                attachLabel.Attach(ec);
                attachLabel.SetBinding(ContentControl.ContentProperty, binding);

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
            if (modelEdge == null) return;
            try
            {
                if (_edgeLabelMap.TryGetValue(modelEdge, out var label) && label != null)
                {
                    try { label.ShowLabel = false; } catch (Exception ex) { Debug.WriteLine("DetachLabelForEdge: ShowLabel=false failed: " + ex.Message); }
                    try { label.Detach(); } catch (Exception ex) { Debug.WriteLine("DetachLabelForEdge: Detach() failed: " + ex.Message); }
                    try { if (LastGraphArea != null && LastGraphArea.Children.Contains(label)) LastGraphArea.Children.Remove(label); } catch (Exception ex) { Debug.WriteLine("DetachLabelForEdge: remove from RootArea failed: " + ex.Message); }
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
            if (!removed) return;

            // if no transitions left -> remove edge from graph and detach label first
            if (modelEdge.Transitions.Count == 0)
            {
                try
                {
                    DetachLabelForEdge(modelEdge);
                    LastGraph.RemoveEdge(modelEdge);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("RemoveTransition: remove edge failed: " + ex.Message);
                }
            }

            try
            {
                LastGraphArea.GenerateAllEdges();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RemoveTransition: GenerateAllEdges failed: " + ex.Message);
            }
        }

        public void AddTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            var from = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.FromState.FullIndex);
            var to = LastGraph.Vertices.FirstOrDefault(v => v.State?.FullIndex == transition.NextState.FullIndex);

            if (from == null || to == null)
            {
                Debug.WriteLine($"AddTransition: source/target vertex not found (from={transition.FromState.FullIndex}, to={transition.NextState.FullIndex})");
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
                var newEdge = new GraphEdge(from, to, new[] { transition });
                LastGraph.AddEdge(newEdge);
            }

            try
            {
                LastGraphArea.GenerateAllEdges();

                foreach (var kv in LastGraphArea.EdgesList)
                {
                    if (kv.Key is GraphEdge modelEdge && kv.Value is EdgeControl ec)
                    {
                        if (!_edgeLabelMap.ContainsKey(modelEdge))
                            AttachBindableLabelToEdge(ec, modelEdge);
                        else
                        {
                            try
                            {
                                var lbl = _edgeLabelMap[modelEdge];
                                if (lbl != null)
                                {
                                    LastGraphArea.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        try { lbl.ShowLabel = true; } catch { }
                                    }), DispatcherPriority.Loaded);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AddTransition: GenerateAllEdges or attach labels failed: " + ex.Message);
            }
        }
    }
}
