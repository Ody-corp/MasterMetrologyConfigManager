/*using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MasterMetrology.Core.Rendering
{
    internal class VisualRendering(Panel viewPort)
    {
        StateModelVisual visual = new StateModelVisual();
        Panel viewPort = viewPort;
        List<Size> statesCords = new List<Size>();
        private readonly OrthogonalRouter router = new();
        private readonly List<Polyline> transitions = new();
        private int counter = 0;

        private readonly Dictionary<string, Grid> stateGridMap = new(); //naviac

        private void RegisterState(StateModelData state, Grid grid) //naviac
        {
            if (!stateGridMap.ContainsKey(state.Index))
                stateGridMap[state.FullIndex] = grid;
        }

        public void RenderStates(List<StateModelData> states, double startX, double startY, double spacingX, double spacingY)
        {

            foreach (StateModelData state in states)
            {
                var size = MeasureState(state, spacingX, spacingY);

                var grid = RenderStateRecursive(state, startX, startY, spacingX, spacingY);
                viewPort.Children.Add(grid);

                startY += size.Height + spacingY;

                state.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(StateModelData.X) || e.PropertyName == nameof(StateModelData.Y))
                        UpdateTransitions();
                };

                //foreach (var transition in state.TransitionsData)
                //    DrawTransition(state, transition, grid);
            }

            //viewPort.Dispatcher.BeginInvoke(UpdateTransitions, DispatcherPriority.Loaded);
            viewPort.Dispatcher.BeginInvoke(() => UpdateTransitions(true), DispatcherPriority.Loaded);

            //UpdateTransitions(true);
        }

        private Grid RenderStateRecursive(StateModelData state, double x, double y, double spacingX, double spacingY)
        {
            Grid stateBox;

            if (state.SubStatesData.Count == 0)
            {
                stateBox = visual.CreateTableData(x, y, state.Name, state.Index);
                stateBox.Tag = state;
                RegisterState(state, stateBox);
            }
            else
            {
                //Config.TEMP_VALUE_X_CORD = x;
                //Config.TEMP_VALUE_Y_CORD = y;

                //Config.TEMP_VALUE_X_CORD += Config.DEFAULT_VALUE_GRID_MARGIN;
                //Config.TEMP_VALUE_Y_CORD += Config.DEFAULT_VALUE_TEXT_MARGIN*2;

                stateBox = visual.CreateSectionData(x, y, state.Name, state.Index);
                stateBox.Tag = state;

                var innerGrid = stateBox.Children[0] as Grid;

                double subY = 0; // offset v rámci parent Gridu
                foreach (var sub in state.SubStatesData)
                {
                    CalculateGlobalCordinates(x, y);
                    var subBox = visual.CreateTableData(0, subY, sub.Name, sub.Index); // pozícia v rámci innerGrid
                    subBox.Tag = sub;
                    RegisterState(state, subBox);
                    innerGrid.Children.Add(RenderStateRecursive(sub, 0, subY, spacingX, spacingY));

                    subY += MeasureState(state, spacingX, spacingY).Height + spacingY; // medzera medzi subStates
                }


            }
            Debug.WriteLine(state.Name + " x: " + x + " y: " + y);
            //Debug.WriteLine(GetGlobalPosition(stateBox));
            return stateBox;
        }

        private Size MeasureState(StateModelData state, double spacingX, double spacingY)
        {
            const double baseWidth = 120;
            const double baseHeight = 60;

            if (state.SubStatesData.Count == 0)
                return new Size(baseWidth, baseHeight);

            double totalHeight = 0;
            double maxWidth = 0;

            foreach (var sub in state.SubStatesData)
            {
                var subSize = MeasureState(sub, spacingX, spacingY);
                totalHeight += subSize.Height + spacingY;
                maxWidth = Math.Max(maxWidth, subSize.Width);
            }

            totalHeight -= spacingY; // odstrániť poslednú medzeru
            return new Size(maxWidth + spacingX * 2, totalHeight + baseHeight);
        }

        private void CalculateGlobalCordinates(double x, double y)
        {

        }

        private void DrawTransition(StateModelData fromState, TransitionModelData transition, Grid fromGrid)
        {
            var toState = FindStateByName(transition.NextStage); // nájde Grid podľa mena
            if (toState == null) return;

            fromGrid.Dispatcher.BeginInvoke(() =>
            {

                //var fromPoint = GetAbsolutePosition(fromGrid, (Canvas)fromGrid.Parent);
                var fromPoint = GetAbsolutePosition(fromGrid, viewPort);
                var toPoint = GetAbsolutePosition(toState, viewPort);

                var fromCenter = new Point(fromPoint.X + fromGrid.ActualWidth / 2,
                                   fromPoint.Y + fromGrid.ActualHeight / 2);

                var toCenter = new Point(toPoint.X + toState.ActualWidth / 2,
                                         toPoint.Y + toState.ActualHeight / 2);

                var path = router.Route(fromCenter, toCenter);

                transition.PathPoints = new ObservableCollection<Point>(path);

                var polyline = new Polyline
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                    Points = new PointCollection(path)
                };

                transitions.Add(polyline);
                viewPort.Children.Add(polyline);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            counter++;
            Debug.WriteLine("Transition counter: " + counter + " | Index fromState: " + fromState.Index + " | Transitions: " +  transition.Input + " => " + transition.NextStage + " | Next State: " + (toState.Tag as StateModelData).Name + " " + (toState.Tag as StateModelData).Index);
        }
        /*
        private void UpdateTransitions(Panel localViewPort, bool clear = false)
        {
            if (clear)
            {
                foreach (var poly in transitions)
                {
                    localViewPort.Children.Remove(poly);
                }
                transitions.Clear();
            }
            
            foreach (var child in localViewPort.Children)
            {
                if (child is Grid grid && grid.Tag is StateModelData state)
                {
                    if (state.TransitionsData.Count > 0)
                    {
                        foreach (var t in state.TransitionsData)
                        {
                            DrawTransition(state, t, grid);
                        }
                    }
                    else
                    {
                        foreach (var subState in grid.Children)
                        {
                            if (subState is Grid subGrid && subGrid.Tag is StateModelData)
                            UpdateTransitions(subGrid);
                        }
                    }
                    
                }
                
            }
        //}
        private void UpdateTransitions(bool clear = false)
        {
            if (clear)
            {
                foreach (var poly in transitions)
                    viewPort.Children.Remove(poly);

                transitions.Clear();
            }

            foreach (var kv in stateGridMap)
            {
                var state = kv.Value.Tag as StateModelData;
                if (state == null) continue;

                foreach (var t in state.TransitionsData)
                {
                    Debug.WriteLine($"{t.Input} -> {t.NextStage}");
                    DrawTransition(state, t, kv.Value);
                }

                    
            }
        }
        /*private Grid? FindStateByName(string index, Panel container)
        {

            //Debug.WriteLine($"Actual index: {index} container: {container.Name}");

            foreach (var child in container.Children)
            {
                if (child is Grid grid && grid.Tag is StateModelData s)
                {
                    if (s.FullIndex.Equals(index))
                        return grid;

                    foreach (var inner in grid.Children)
                    {
                        if (inner is StackPanel innerPanel)
                        {
                            var result = FindStateByName(index, innerPanel);
                            if (result != null)
                                return result;
                        }
                        else if (inner is Grid innerGrid)
                        {
                            return innerGrid;
                        }
                    }
                }
            }
            return null;
        //}/
        private Grid? FindStateByName(string index)
        {
            Debug.WriteLine($"FindStateByName{stateGridMap.TryGetValue(index, out var gridd)}");
            return stateGridMap.TryGetValue(index, out var grid) ? grid : null;
        }

        private Point GetAbsolutePosition(UIElement element, Visual relativeTo)
        {
            return element.TransformToAncestor(relativeTo).Transform(new Point(0, 0));
        }

        /*
        private Point GetAbsolutePosition(UIElement element, UIElement relativeTo)
        {
            

            // získa pozíciu elementu voči oknu
            var pointInWindow = element.TransformToAncestor(Application.Current.MainWindow)
                                       .Transform(new Point(0, 0));

            // pretransformuje do súradníc voči Canvasu
            return Application.Current.MainWindow.TransformToDescendant(relativeTo)
                                            .Transform(pointInWindow);
        }

        private Point GetGlobalPosition(UIElement element)
        {
            // absolútne súradnice na obrazovke
            return element.PointToScreen(new Point(0, 0));
        //}/
    }
}*/
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MasterMetrology.Core.Rendering
{
    internal class VisualRendering(Panel viewPort)
    {
        private readonly StateModelVisual visual = new();
        private readonly Panel viewPort = viewPort;
        private readonly OrthogonalRouter router = new();
        private readonly List<Polyline> transitions = new();
        private readonly Dictionary<string, Grid> stateGridMap = new();
        private int counter = 0;

        private void RegisterState(StateModelData state, Grid grid)
        {
            if (!stateGridMap.ContainsKey(state.FullIndex))
                stateGridMap[state.FullIndex] = grid;
        }

        public void RenderStates(List<StateModelData> states, double startX, double startY, double spacingX, double spacingY)
        {
            foreach (var state in states)
            {
                RenderStateRecursive(state, startX, startY, spacingX, spacingY);
                startY += MeasureState(state, spacingX, spacingY).Height + spacingY;
            }

            viewPort.Dispatcher.BeginInvoke(() => UpdateTransitions(true), DispatcherPriority.Loaded);
        }

        private void RenderStateRecursive(StateModelData state, double x, double y, double spacingX, double spacingY)
        {
            if (state.SubStatesData.Count == 0)
            {
                var grid = visual.CreateTableData(x, y, state.Name, state.Index);
                grid.Tag = state;
                RegisterState(state, grid);
                viewPort.Children.Add(grid);
                return;
            }

            var subStateBoxes = new List<Grid>();
            double subY = y + 40;
            foreach (var sub in state.SubStatesData)
            {
                RenderStateRecursive(sub, x + 40, subY, spacingX, spacingY);
                var subBox = FindStateByName(sub.FullIndex);
                if (subBox != null)
                    subStateBoxes.Add(subBox);

                subY += MeasureState(sub, spacingX, spacingY).Height + spacingY;
            }

            if (subStateBoxes.Count > 0)
            {
                double minX = subStateBoxes.Min(b => Canvas.GetLeft(b));
                double minY = subStateBoxes.Min(b => Canvas.GetTop(b));
                double maxX = subStateBoxes.Max(b => Canvas.GetLeft(b) + b.ActualWidth);
                double maxY = subStateBoxes.Max(b => Canvas.GetTop(b) + b.ActualHeight);

                //Rect bounds = new Rect(minX - 20, minY - 40, (maxX - minX) + 40, (maxY - minY) + 60);
                Rect bounds = new Rect(minX - 40, minY -40, (maxX - minX) + 40, (maxY - minY) + 40);
                var border = visual.CreateSectionBorder(state.Name, state.Index, bounds);
                border.Tag = state;
                viewPort.Children.Add(border);
            }
        }

        private Size MeasureState(StateModelData state, double spacingX, double spacingY)
        {
            const double baseWidth = 120;
            const double baseHeight = 60;

            if (state.SubStatesData.Count == 0)
                return new Size(baseWidth, baseHeight);

            double totalHeight = 0;
            double maxWidth = 0;

            foreach (var sub in state.SubStatesData)
            {
                var subSize = MeasureState(sub, spacingX, spacingY);
                totalHeight += subSize.Height + spacingY;
                maxWidth = Math.Max(maxWidth, subSize.Width);
            }

            totalHeight -= spacingY;
            return new Size(maxWidth + spacingX * 2, totalHeight + baseHeight);
        }

        private void DrawTransition(StateModelData fromState, TransitionModelData transition, Grid fromGrid)
        {
            var toState = FindStateByName(transition.NextStage);
            if (toState == null) return;

            fromGrid.Dispatcher.BeginInvoke(() =>
            {
                var fromPoint = GetAbsolutePosition(fromGrid, viewPort);
                var toPoint = GetAbsolutePosition(toState, viewPort);

                var fromCenter = new Point(fromPoint.X + fromGrid.ActualWidth / 2,
                                           fromPoint.Y + fromGrid.ActualHeight / 2);

                var toCenter = new Point(toPoint.X + toState.ActualWidth / 2,
                                         toPoint.Y + toState.ActualHeight / 2);

                var path = router.Route(fromCenter, toCenter);
                transition.PathPoints = new ObservableCollection<Point>(path);

                var polyline = new Polyline
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                    Points = new PointCollection(path)
                };

                transitions.Add(polyline);
                viewPort.Children.Add(polyline);
            }, DispatcherPriority.Loaded);

            counter++;
            Debug.WriteLine($"Transition {counter}: {fromState.Index} -> {transition.NextStage}");
        }

        private void UpdateTransitions(bool clear = false)
        {
            if (clear)
            {
                foreach (var poly in transitions)
                    viewPort.Children.Remove(poly);
                transitions.Clear();
            }

            foreach (var kv in stateGridMap)
            {
                var state = kv.Value.Tag as StateModelData;
                if (state == null) continue;

                foreach (var t in state.TransitionsData)
                    DrawTransition(state, t, kv.Value);
            }
        }

        private Grid? FindStateByName(string index)
        {
            return stateGridMap.TryGetValue(index, out var grid) ? grid : null;
        }

        private Point GetAbsolutePosition(UIElement element, Visual relativeTo)
        {
            return element.TransformToAncestor(relativeTo).Transform(new Point(0, 0));
        }
    }
}
