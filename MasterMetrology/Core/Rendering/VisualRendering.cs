using GraphX.Controls;
using GraphX.PCL.Common.Models;
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
using GraphX.PCL.Common.Interfaces;
using GraphX.PCL.Logic.Models;
using GraphX.PCL.Logic.Algorithms.LayoutAlgorithms;
using GraphX.PCL.Logic.Algorithms.OverlapRemoval;
using MasterMetrology.Utils;
using System.Windows.Input;

namespace MasterMetrology.Core.Rendering
{
    internal class VisualRendering
    {
        private GraphVertex _selectedVertex;
        public StateGraphArea LastGraphArea { get; private set; }
        public StateGraph LastGraph { get; private set; }

        private Dictionary<string, GraphVertex> _vertexMap = new();

        // mapovanie modelEdge -> attachable label control
        private readonly Dictionary<GraphEdge, AttachableEdgeLabelControl> _edgeLabelMap = new Dictionary<GraphEdge, AttachableEdgeLabelControl>();

        private EventHandler _layoutUpdatedHandler;

        private readonly Dictionary<string, VertexControl> _vcByFullIndex = new();
        private readonly Dictionary<string, Size> _sizeByFullIndex = new();

        private readonly SectionAwareEdgeRouter _edgeRouter = new SectionAwareEdgeRouter()
        {
            CellSize = 40,
            WorldPadding = 250,
            LeafMargin = 14,
            SectionMargin = 18,
        };

        private List<StateModelData> _lastRoots; // aby si vedel reroutovať aj pri Add/RemoveTransition
        private readonly GraphInteractionController _interaction = new GraphInteractionController();

        private readonly Dictionary<GraphEdge, BaseEdgeVisualState> _edgeBase = new();
        private readonly HashSet<GraphEdge> _hovered = new();

        private bool _suppressVisualRefresh;

        public void RenderGraph(List<StateModelData> states, Canvas graphLayer, Action<GraphVertex?> onVertexSelected = null, Canvas diagramCanvas = null)
        {
            CleanupLastGraphArea(graphLayer);

            graphLayer.Children.Clear();

            _lastRoots = states;

            var graph = new StateGraph();
            LastGraph = graph;

            _vertexMap = new Dictionary<string, GraphVertex>();

            foreach (var state in states)
                AddStateRecursive(state, graph, _vertexMap);

            foreach (var state in states)
                AddTransitionsRecursive(state, graph, _vertexMap);

            var logicCore = new StateLogicCore
            {
                Graph = graph,
                DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.SimpleRandom,
                DefaultOverlapRemovalAlgorithm = OverlapRemovalAlgorithmTypeEnum.OneWayFSA,
                DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.None,
                EnableParallelEdges = true,
                EdgeCurvingEnabled = false,
                
            };

            var factory = new CustomControlFactory();

            logicCore.EdgeCurvingEnabled = false;

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

            // aby Canvas vždy chytal klik (aj keď je "prázdny")
            if (graphLayer.Background == null)
                graphLayer.Background = Brushes.Transparent;

            // CLEAR selection pri kliknutí mimo interaktívnych prvkov
            diagramCanvas.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler((s, e) =>
                {
                    Debug.WriteLine("BACKGROUND CLICK");
                    var src = e.OriginalSource as DependencyObject;

                    if (!IsInteractiveClick(src))
                    {
                        ClearSelectionAndHighlights(onVertexSelected);
                    }
                }),
                true);

            // generate graph (do NOT call ShowAllEdgesLabels here - manage labels ourselves)
            graphArea.GenerateGraph(true);
            graphArea.SetVerticesDrag(true, true);
            
            // Attach labels for generated edges (after GenerateGraph)
            var dispatcher = graphArea.Dispatcher ?? Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    graphArea.UpdateLayout();

                    BuildVertexControlMap(graphArea);

                    //ApplyCustomLayout(states, graphArea);

                    _sizeByFullIndex.Clear();
                    foreach (var r in states.OrderBy(s => s.FullIndex, FullIndexComparer.Instance))
                    {
                        ComputeDesiredSizeRecursive(r);
                    }

                    ApplySectionSizes(graphArea);
                    graphArea.UpdateLayout();
                    //graphArea.GenerateAllEdges();

                    LayoutRootColumns(states, graphArea);
                    graphArea.UpdateLayout();

                    foreach (var r in states)
                    {
                        PlaceChildrenRecursive(r, graphArea);
                    }

                    graphArea.UpdateLayout();
                    graphArea.GenerateAllEdges();
                    graphArea.UpdateLayout();

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


                    // --- CUSTOM ROUTING (section-aware) ---
                    _edgeRouter.RebuildCaches(states, graphArea);   // potrebuje roots + finálne recty
                    _edgeRouter.RouteAllEdges(graphArea);           // nastaví RoutingPoints + UpdateEdge()

                    graphArea.UpdateLayout(); // nech sa dobehne vizuál (labely/arrange)

                    InstallEdgeAndVertexHighlighting(graphArea, onVertexSelected);

                    RefreshEdgeVisuals();

                    _interaction.Attach(graphArea, states, _edgeRouter);

                    

                    /*foreach (var vc in graphArea.VertexList.Values)
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
                    }*/
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Custom layout pipeline failed: {ex.Message}");
                }

            }
            ), DispatcherPriority.Loaded);
            
            

            //UpdateSubPositions(graphArea, graph);

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
                    {
                        sectionVertex.SubVertices.Add(child);
                    }
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
            if (state.TransitionsData != null && state.TransitionsData.Count > 0)
            {
                var grouped = state.TransitionsData.GroupBy(t => t.NextState.FullIndex);
                foreach (var g in grouped)
                {
                    if (!map.TryGetValue(state.FullIndex, out var from))
                    {
                        Debug.WriteLine($"Missing FROM: {state.FullIndex}");
                        continue;
                    }

                    if (!map.TryGetValue(g.Key, out var to))
                    {
                        Debug.WriteLine($"Missing TO: {g.Key} (from {state.FullIndex})");
                        continue;
                    }

                    var transitionsForThisPair = g.ToList();
                    var edge = new GraphEdge(from, to, transitionsForThisPair);
                    graph.AddEdge(edge);
                }
            }

            if (state.SubStatesData != null)

            {
                foreach (var sub in state.SubStatesData)
                    AddTransitionsRecursive(sub, graph, map);
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
                    Opacity = 1
                    // don't set ShowLabel here — set it after attach + dispatcher
                };

                var binding = new Binding(nameof(GraphEdge.Text))
                {
                    Source = modelEdge,
                    Mode = BindingMode.OneWay
                };
                attachLabel.Attach(ec);
                attachLabel.SetBinding(ContentControl.ContentProperty, binding);

                attachLabel.MouseEnter += (s, e) => SetHover(modelEdge, true);
                attachLabel.MouseLeave += (s, e) => SetHover(modelEdge, false);

                _edgeLabelMap[modelEdge] = attachLabel;

                CacheBaseStyleIfMissing(modelEdge, ec, attachLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AttachBindableLabelToEdge failed: " + ex.Message);
            }
        }

        private void CacheBaseStyleIfMissing(GraphEdge edge, EdgeControl ec, AttachableEdgeLabelControl lbl)
        {
            if (_edgeBase.ContainsKey(edge)) return;

            // EdgeControl má väčšinou Path v template – ale na jednoduché nastavenia stačí:
            var baseState = new BaseEdgeVisualState
            {
                Stroke = ec.Foreground ?? Brushes.Black,
                Thickness = ec.StrokeThickness > 0 ? ec.StrokeThickness : 1.0,
                Opacity = ec.Opacity,

                LabelForeground = lbl.Foreground ?? Brushes.Black,
                LabelBackground = lbl.Background ?? Brushes.White,
                LabelBorderBrush = lbl.BorderBrush ?? Brushes.Black,
                LabelOpacity = lbl.Opacity
            };

            _edgeBase[edge] = baseState;
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
                _suppressVisualRefresh = true;

                LastGraphArea.GenerateAllEdges();

                if (_lastRoots != null)
                {
                    LastGraphArea.UpdateLayout();
                    _edgeRouter.RebuildCaches(_lastRoots, LastGraphArea);
                    _edgeRouter.RouteAllEdges(LastGraphArea);
                    LastGraphArea.UpdateLayout();
                }

                LastGraphArea.Dispatcher.BeginInvoke(new Action(() =>
                {
                   ReinstallHoverHandlers();
                    _suppressVisualRefresh = false;
                    RefreshEdgeVisuals();
                }), DispatcherPriority.Render);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("RemoveTransition: GenerateAllEdges failed: " + ex.Message);
            }

            

        }

        public void AddTransition(TransitionModelData transition)
        {
            if (LastGraphArea == null || LastGraph == null || transition == null) return;

            if (!_vertexMap.TryGetValue(transition.FromState.FullIndex, out var from) ||
                !_vertexMap.TryGetValue(transition.NextState.FullIndex, out var to))
            {
                Debug.WriteLine($"AddTransition: vertex not found in map (from={transition.FromState.FullIndex}, to={transition.NextState.FullIndex})");
                return;
            }
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
                _suppressVisualRefresh = true;

                LastGraphArea.GenerateAllEdges();

                if (_lastRoots != null)
                {
                    LastGraphArea.UpdateLayout();
                    _edgeRouter.RebuildCaches(_lastRoots, LastGraphArea);
                    _edgeRouter.RouteAllEdges(LastGraphArea);
                    LastGraphArea.UpdateLayout();
                }


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

            LastGraphArea.Dispatcher.BeginInvoke(new Action(() =>
            {
                ReinstallHoverHandlers();
                _suppressVisualRefresh = false;
                RefreshEdgeVisuals();
            }), DispatcherPriority.Render);

        }

        private void ApplyCustomLayout(List<StateModelData> roots, StateGraphArea area)
        {
            const double ROOT_GAP_X = 120;
            const double ROOT_GAP_Y = 60;
            const double START_X = 60;
            const double START_Y = 60;

            const double SECTION_INNER_PAD_X = 18;
            const double SECTION_INNER_PAD_Y = 16;
            const double SECTION_HEADER_H = 40;
            const double SECTION_CHILD_GAP_X = 24;
            const double SECTION_CHILD_GAP_Y = 20;

            double maxColumnHeight = 1800;

            var orderedRoots = roots.OrderBy(m => m.FullIndex, FullIndexComparer.Instance).ToList();
            LayoutRootsMultiColumn(orderedRoots, area, START_X, START_Y, ROOT_GAP_X, ROOT_GAP_Y, maxColumnHeight);

            // 2) pre každú sekciu rozlož jej subStates dovnútra + uprav veľkosť sekcie
            //    (musí sa robiť po tom, čo sekcia už má pozíciu)
            foreach (var root in orderedRoots)
                ApplySectionLayoutRecursive(root, area,
                    SECTION_HEADER_H, SECTION_INNER_PAD_X, SECTION_INNER_PAD_Y, SECTION_CHILD_GAP_X, SECTION_CHILD_GAP_Y);

        }
        private void LayoutRootsMultiColumn(List<StateModelData> roots, StateGraphArea area, double startX, double startY, double gapX, double gapY, double maxColumnHeight)
        {
            double x = startX;
            double y = startY;

            double columnMaxWidth = 0;

            foreach (var model in roots)
            {
                if (!TryGetVertexControlByFullIndex(area, model.FullIndex, out var vc)) continue;

                var size = GetControlSize(vc);
                double w = size.Width;
                double h = size.Height;

                // wrap do ďalšieho stĺpca
                if (y + h > startY + maxColumnHeight)
                {
                    x += columnMaxWidth + gapX;
                    y = startY;
                    columnMaxWidth = 0;
                }

                vc.SetPosition(x, y);

                y += h + gapY;
                if (w > columnMaxWidth) columnMaxWidth = w;
            }
        }
        private void ApplySectionLayoutRecursive(StateModelData model, StateGraphArea area, double headerH, double padX, double padY, double childGapX, double childGapY)
        {
            if (model == null) return;

            // ak je to sekcia (má subStates), rozlož jej deti dovnútra
            if (model.SubStatesData != null && model.SubStatesData.Count > 0)
            {
                LayoutSectionContents(model, area, headerH, padX, padY, childGapX, childGapY);
            }

            // rekurzia na submodely (kvôli nested sekciám)
            if (model.SubStatesData != null)
            {
                foreach (var sub in model.SubStatesData)
                    ApplySectionLayoutRecursive(sub, area, headerH, padX, padY, childGapX, childGapY);
            }
        }
        private void LayoutSectionContents(StateModelData sectionModel, StateGraphArea area, double headerH, double padX, double padY, double gapX, double gapY)
        {
            if (!TryGetVertexControlByFullIndex(area, sectionModel.FullIndex, out var vcSection)) return;

            // musí to byť SectionVertexControl, inak nemá zmysel resize
            if (vcSection is not SectionVertexControl sectionControl) return;

            // pozícia sekcie
            var secPos = vcSection.GetPosition();
            double secX = secPos.X;
            double secY = secPos.Y;

            // zoradené deti podľa FullIndex
            var children = sectionModel.SubStatesData
                .OrderBy(m => m.FullIndex, FullIndexComparer.Instance)
                .ToList();

            // vnútorný layout box
            double innerStartX = secX + padX;
            double innerStartY = secY + headerH + padY;

            // max výška “vnútorného stĺpca”
            // podľa aktuálnej výšky sekcie (ak je default 180, môže byť málo)
            // dáme minimálne 400, alebo niečo rozumné
            double innerMaxHeight = Math.Max(400, sectionControl.SectionHeight - headerH - (padY * 2));

            double x = innerStartX;
            double y = innerStartY;
            double colMaxWidth = 0;

            double usedMaxRight = innerStartX;
            double usedMaxBottom = innerStartY;

            foreach (var childModel in children)
            {
                if (!TryGetVertexControlByFullIndex(area, childModel.FullIndex, out var vcChild)) continue;

                var size = GetControlSize(vcChild);
                double w = size.Width;
                double h = size.Height;

                // wrap do ďalšieho stĺpca vnútri sekcie
                if (y + h > innerStartY + innerMaxHeight)
                {
                    x += colMaxWidth + gapX;
                    y = innerStartY;
                    colMaxWidth = 0;
                }

                vcChild.SetPosition(x, y);

                y += h + gapY;
                if (w > colMaxWidth) colMaxWidth = w;

                usedMaxRight = Math.Max(usedMaxRight, x + w);
                usedMaxBottom = Math.Max(usedMaxBottom, y);
            }

            // resize sekcie aby obsiahla deti + padding
            double requiredWidth = (usedMaxRight - secX) + padX;
            double requiredHeight = (usedMaxBottom - secY) + padY;

            // minimá aby sekcia nebola príliš malá
            requiredWidth = Math.Max(requiredWidth, 260);
            requiredHeight = Math.Max(requiredHeight, 180);

            // ak zmeníme veľkosť, je dobré to spraviť pred ďalšími prepočtami
            sectionControl.SetSize(requiredWidth, requiredHeight);
        }
        private bool TryGetVertexControlByFullIndex(StateGraphArea area, string fullIndex, out VertexControl vc)
        {
            vc = null;
            if (area?.VertexList == null || string.IsNullOrWhiteSpace(fullIndex)) return false;

            // VertexList: Dictionary<IVertex, VertexControl>
            foreach (var kv in area.VertexList)
            {
                if (kv.Key is GraphVertex gv && gv.State?.FullIndex == fullIndex)
                {
                    vc = kv.Value;
                    return true;
                }
            }
            return false;
        }
        private Size GetControlSize(VertexControl vc)
        {
            if (vc == null) return new Size(80, 40);

            double w = vc.ActualWidth > 0 ? vc.ActualWidth : (vc.Width > 0 ? vc.Width : 80);
            double h = vc.ActualHeight > 0 ? vc.ActualHeight : (vc.Height > 0 ? vc.Height : 40);

            return new Size(w, h);
        }
        private void BuildVertexControlMap(StateGraphArea area)
        {
            _vcByFullIndex.Clear();
            foreach (var kv in area.VertexList)
            {
                if (kv.Key is GraphVertex gv && gv.State?.FullIndex != null)
                    _vcByFullIndex[gv.State.FullIndex] = kv.Value;
            }
        }

        private bool TryGetVC(string fullIndex, out VertexControl vc)
            => _vcByFullIndex.TryGetValue(fullIndex, out vc);

        private static Size MeasuredSize(VertexControl vc)
        {
            double w = vc.ActualWidth > 0 ? vc.ActualWidth : (vc.Width > 0 ? vc.Width : 80);
            double h = vc.ActualHeight > 0 ? vc.ActualHeight : (vc.Height > 0 ? vc.Height : 40);
            return new Size(w, h);
        }
        private Size ComputeDesiredSizeRecursive(StateModelData model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.FullIndex))
                return new Size(120, 60);

            // ak je to list (nemá subStates) -> size z merania kontrolu
            if (model.SubStatesData == null || model.SubStatesData.Count == 0)
            {
                if (TryGetVC(model.FullIndex, out var vcLeaf))
                {
                    var s = MeasuredSize(vcLeaf);
                    _sizeByFullIndex[model.FullIndex] = s;
                    return s;
                }
                _sizeByFullIndex[model.FullIndex] = new Size(120, 60);
                return new Size(120, 60);
            }

            // sekcia: najprv spočítaj sizes detí
            var children = model.SubStatesData
                .OrderBy(m => m.FullIndex, FullIndexComparer.Instance)
                .ToList();

            var childSizes = new List<(StateModelData m, Size s)>();
            foreach (var ch in children)
            {
                var cs = ComputeDesiredSizeRecursive(ch);
                childSizes.Add((ch, cs));
            }

            // --- layout “na sucho” (bez pozícií) len pre výpočet potrebnej veľkosti ---
            const double HEADER_H = 40;
            const double PAD_X = 18;
            const double PAD_Y = 16;
            const double GAP_X = 24;
            const double GAP_Y = 20;

            // výška jedného vnútorného stĺpca – fixná (nie podľa starej SectionHeight!)
            // doladíš podľa veľkosti grafu
            const double INNER_MAX_H = 700;

            double x = 0, y = 0, colMaxW = 0;
            double usedMaxRight = 0;
            double usedMaxBottom = 0;

            foreach (var (_, s) in childSizes)
            {
                if (y + s.Height > INNER_MAX_H && y > 0)
                {
                    x += colMaxW + GAP_X;
                    y = 0;
                    colMaxW = 0;
                }

                // “položenie” vnútri sekcie (relatívne)
                usedMaxRight = Math.Max(usedMaxRight, x + s.Width);
                usedMaxBottom = Math.Max(usedMaxBottom, y + s.Height);

                y += s.Height + GAP_Y;
                colMaxW = Math.Max(colMaxW, s.Width);
            }

            double requiredW = PAD_X * 2 + usedMaxRight;
            double requiredH = HEADER_H + PAD_Y * 2 + usedMaxBottom;

            // minimá
            requiredW = Math.Max(requiredW, 260);
            requiredH = Math.Max(requiredH, 180);

            var secSize = new Size(requiredW, requiredH);
            _sizeByFullIndex[model.FullIndex] = secSize;
            return secSize;
        }
        private void ApplySectionSizes(StateGraphArea area)
        {
            foreach (var kv in _sizeByFullIndex)
            {
                if (!TryGetVC(kv.Key, out var vc)) continue;

                if (vc is SectionVertexControl sec)
                {
                    sec.SetSize(kv.Value.Width, kv.Value.Height);
                }
            }
        }
        private void LayoutRootColumns(List<StateModelData> roots, StateGraphArea area)
        {
            const double START_X = 60;
            const double START_Y = 60;
            const double GAP_X = 140;
            const double GAP_Y = 70;

            const double MAX_COL_H = 1800; // wrap do ďalšieho stĺpca

            double x = START_X, y = START_Y;
            double colMaxW = 0;

            foreach (var r in roots.OrderBy(m => m.FullIndex, FullIndexComparer.Instance))
            {
                if (!TryGetVC(r.FullIndex, out var vc)) continue;

                // použijeme vypočítanú veľkosť (lepšie ako Actual pri sekciách)
                var size = _sizeByFullIndex.TryGetValue(r.FullIndex, out var s) ? s : MeasuredSize(vc);

                if (y + size.Height > START_Y + MAX_COL_H && y > START_Y)
                {
                    x += colMaxW + GAP_X;
                    y = START_Y;
                    colMaxW = 0;
                }

                vc.SetPosition(x, y);

                y += size.Height + GAP_Y;
                colMaxW = Math.Max(colMaxW, size.Width);
            }
        }
        private void PlaceChildrenRecursive(StateModelData model, StateGraphArea area)
        {
            if (model?.SubStatesData == null || model.SubStatesData.Count == 0)
                return;

            if (!TryGetVC(model.FullIndex, out var vcSection)) return;

            var secPos = vcSection.GetPosition();
            double secX = secPos.X;
            double secY = secPos.Y;

            const double HEADER_H = 40;
            const double PAD_X = 18;
            const double PAD_Y = 16;
            const double GAP_X = 24;
            const double GAP_Y = 20;
            const double INNER_MAX_H = 700; // rovnaké ako v ComputeDesiredSizeRecursive

            double startX = secX + PAD_X;
            double startY = secY + HEADER_H + PAD_Y;

            double x = startX, y = startY;
            double colMaxW = 0;

            var children = model.SubStatesData.OrderBy(m => m.FullIndex, FullIndexComparer.Instance).ToList();

            foreach (var ch in children)
            {
                if (!TryGetVC(ch.FullIndex, out var vcChild)) continue;

                var size = _sizeByFullIndex.TryGetValue(ch.FullIndex, out var s) ? s : MeasuredSize(vcChild);

                // wrap vnútri sekcie
                if ((y - startY) + size.Height > INNER_MAX_H && y > startY)
                {
                    x += colMaxW + GAP_X;
                    y = startY;
                    colMaxW = 0;
                }

                vcChild.SetPosition(x, y);

                y += size.Height + GAP_Y;
                colMaxW = Math.Max(colMaxW, size.Width);
            }

            // rekurzia – ak je dieťa sekcia, musí uložiť aj svoje deti po tom, čo sa umiestni
            foreach (var ch in children)
                PlaceChildrenRecursive(ch, area);
        }

        private void SetHover(GraphEdge edge, bool isHover)
        {
            if (isHover) _hovered.Add(edge);
            else _hovered.Remove(edge);

            if (!_suppressVisualRefresh) RefreshEdgeVisuals();
        }
        private void ApplyEdgeStyle(
            GraphEdge edge, 
            Brush stroke, 
            double thickness, 
            double opacity,
            Brush labelFg, 
            Brush labelBg, 
            Brush labelBorder, 
            double labelOpacity)
        {
            if (!LastGraphArea.EdgesList.TryGetValue(edge, out var ecObj)) return;
            if (ecObj is not EdgeControl ec) return;

            ec.Foreground = stroke;
            ec.StrokeThickness = thickness;
            ec.Opacity = opacity;
            ec.UpdateEdge();

            // LABELS (použi EdgeLabelControls, nie len _edgeLabelMap)
            if (ec.GetLabelControls() != null)
            {
                foreach (var c in ec.GetLabelControls())
                {
                    if (c is FrameworkElement fe)
                        fe.Opacity = labelOpacity;

                    // ak je to ContentControl (AttachableEdgeLabelControl je ContentControl)
                    if (c is ContentControl cc)
                    {
                        cc.Foreground = labelFg;
                        cc.Background = labelBg;
                        cc.BorderBrush = labelBorder;
                    }
                }
            }
        }

        private void RefreshEdgeVisuals()
        {
            if (LastGraphArea == null || LastGraph == null) return;
            if (_suppressVisualRefresh) return;

            bool hasHover = _hovered.Count > 0;

            // zisti incoming/outgoing pre selected
            var outgoing = new HashSet<GraphEdge>();
            var incoming = new HashSet<GraphEdge>();

            if (_selectedVertex != null)
            {
                foreach (var e in LastGraph.Edges.OfType<GraphEdge>())
                {
                    if (e.Source == _selectedVertex) outgoing.Add(e);
                    if (e.Target == _selectedVertex) incoming.Add(e);
                }
            }

            foreach (var e in LastGraph.Edges.OfType<GraphEdge>())
            {
                if (!LastGraphArea.EdgesList.TryGetValue(e, out var ecObj) || ecObj is not EdgeControl ec) continue;
                _edgeLabelMap.TryGetValue(e, out var lbl);

                // base (fallback keď ešte nie je)
                if (!_edgeBase.TryGetValue(e, out var b))
                {
                    lbl ??= null;
                    CacheBaseStyleIfMissing(e, ec, lbl);
                    b = _edgeBase[e];
                }

                // default je base
                Brush stroke = b.Stroke;
                double thick = b.Thickness;
                double op = b.Opacity;

                Brush lfg = b.LabelForeground;
                Brush lbg = b.LabelBackground;
                Brush lbr = b.LabelBorderBrush;
                double lop = b.LabelOpacity;

                bool isHovered = _hovered.Contains(e);

                if (hasHover && !isHovered)
                {
                    op = 0.12;
                    lop = 0.12;
                }

                // selection (vertex)
                if (_selectedVertex != null)
                {
                    bool related = outgoing.Contains(e) || incoming.Contains(e);

                    if (!related)
                    {
                        op = 0.12;
                        lop = 0.12;
                    }
                    else if (outgoing.Contains(e))
                    {
                        stroke = Brushes.Green;
                        thick = Math.Max(2.0, b.Thickness);
                        lfg = Brushes.Green;
                        lbr = Brushes.Green;
                        op = 1;
                        lop = 1;
                    }
                    else if (incoming.Contains(e))
                    {
                        stroke = Brushes.Red;
                        thick = Math.Max(2.0, b.Thickness);
                        lfg = Brushes.Red;
                        lbr = Brushes.Red;
                        op = 1;
                        lop = 1;
                    }
                }

                // hover má PRIORITU nad všetkým (vždy modrá)
                if (_hovered.Contains(e))
                {
                    stroke = Brushes.DodgerBlue;
                    thick = Math.Max(3.0, thick);
                    op = 1.0;

                    lfg = Brushes.DodgerBlue;
                    lbr = Brushes.DodgerBlue;
                    lop = 1.0;
                }

                ApplyEdgeStyle(e, stroke, thick, op, lfg, lbg, lbr, lop);
            }
        }
        private void InstallEdgeAndVertexHighlighting(StateGraphArea graphArea, Action<GraphVertex?>? onVertexSelected)
        {
            if (graphArea?.EdgesList == null || graphArea.VertexList == null) return;

            // EDGE hover (cez EdgeControl)
            foreach (var kv in graphArea.EdgesList)
            {
                if (kv.Key is not GraphEdge ge) continue;
                if (kv.Value is not EdgeControl ec) continue;

                ec.MouseEnter += (_, __) => SetHover(ge, true);
                ec.MouseLeave += (_, __) => SetHover(ge, false);

                // klik na hranu nech nepadá do "background clear"
                ec.MouseLeftButtonDown += (_, e) => e.Handled = true;
                //ec.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
            }

            // LABEL hover (ak niekedy label "odchytí" myš)
            foreach (var kv in graphArea.EdgesList)
            {
                if (kv.Key is not GraphEdge ge) continue;
                if (kv.Value is not EdgeControl ec) continue;

                var labels = ec.GetLabelControls();
                if (labels == null) continue;

                foreach (var l in labels)
                {
                    if (l is UIElement ui)
                    {
                        ui.MouseEnter += (_, __) => SetHover(ge, true);
                        ui.MouseLeave += (_, __) => SetHover(ge, false);
                        //ui.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
                        ui.MouseLeftButtonDown += (_, e) => e.Handled = true; // aby sa neclearlo pozadie
                    }
                }
            }

            // VERTEX click selection
            foreach (var vc in graphArea.VertexList.Values)
            {
                vc.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true; // dôležité

                vc.MouseLeftButtonUp += (_, e) =>
                {
                    if (vc.Vertex is not GraphVertex gv) return;

                    _selectedVertex = gv;
                    onVertexSelected?.Invoke(gv);

                    RefreshEdgeVisuals();
                    e.Handled = true;
                };

                // aby MouseDown na vertex nepustil event až na graphArea
                vc.MouseLeftButtonDown += (_, e) => e.Handled = true;
            }
        }
        private void ClearSelectionAndHighlights(Action<GraphVertex>? onVertexSelected)
        {
            _selectedVertex = null;
            _hovered.Clear();               // zruš aj hover stav
            onVertexSelected?.Invoke(null); // vynuluj panel
            RefreshEdgeVisuals();           // vráti farby/opacities na base
        }
        private static bool HasAncestor<T>(DependencyObject? start) where T : DependencyObject
        {
            while (start != null)
            {
                if (start is T) return true;
                start = VisualTreeHelper.GetParent(start);
            }
            return false;
        }

        private static bool IsInteractiveClick(DependencyObject? src)
        {
            while (src != null)
            {
                // Vertex
                if (src is VertexControl) return true;

                // Edge
                if (src is EdgeControl) return true;

                // Label (v GraphX je to často niečo čo implementuje IEdgeLabelControl)
                if (src is IEdgeLabelControl) return true;

                // pre istotu aj konkrétny attachable label
                if (src is AttachableEdgeLabelControl) return true;

                src = VisualTreeHelper.GetParent(src);
            }
            return false;
        }

        private void ReinstallHoverHandlers()
        {
            if (LastGraphArea?.EdgesList == null) return;

            _hovered.Clear();

            foreach (var kv in LastGraphArea.EdgesList)
            {
                if (kv.Key is not GraphEdge ge) continue;
                if (kv.Value is not EdgeControl ec) continue;

                ec.MouseEnter += (_, __) => SetHover(ge, true);
                ec.MouseLeave += (_, __) => SetHover(ge, false);

                ec.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    //SetHover(ge, true);
                    e.Handled = true;
                };

                // label hover
                var labels = ec.GetLabelControls();
                if (labels == null) continue;

                foreach (var l in labels)
                {
                    if (l is UIElement ui)
                    {
                        ui.MouseEnter += (_, __) => SetHover(ge, true);
                        ui.MouseLeave += (_, __) => SetHover(ge, false);

                        ui.PreviewMouseLeftButtonDown += (_, e) =>
                        {
                            //SetHover(ge, true);
                            e.Handled = true;
                        };
                    }
                }
            }

            RefreshEdgeVisuals();
        }

    }
}
