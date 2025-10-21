using GraphX;
using GraphX.Controls;
using GraphX.PCL.Common.Enums;
using MasterMetrology.Controls;
using MasterMetrology.Core.GraphX;
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
        public void RenderGraph(List<StateModelData> states, Canvas graphLayer)
        {
            graphLayer.Children.Clear();

            // 1️⃣ Vytvoríme GraphX graf
            var graph = new StateGraph();

            // Mapovanie state -> vertex
            var vertexMap = new Dictionary<string, GraphVertex>();

            // 2️⃣ Najprv pridaj všetky stavy ako uzly
            foreach (var state in states)
                AddStateRecursive(state, graph, vertexMap);
            foreach (var element in vertexMap)
            {
            //    Debug.WriteLine($"{element.Key} - {element.Value}");
            }
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
                EnableParallelEdges = false
            };

            // 5️⃣ Vytvor GraphArea a vygeneruj vizuál
            var graphArea = new StateGraphArea
            {
                LogicCore = logicCore,
                Width = Config.DEFAULT_VALUE_CANVAS_X,
                Height = Config.DEFAULT_VALUE_CANVAS_Y,
                Background = Brushes.Transparent,
                
            };

             
            

            foreach (var vc in graphArea.VertexList.Values)
            {
                var vertex = vc.Vertex as GraphVertex;
                if (vertex == null) continue;

                
                    if (vertex is GraphVertexSection)
                    {
                        // 👉 Sekcia – použije SectionVertexTemplate z App.xaml
                        vc.SetResourceReference(ContentControl.ContentTemplateProperty, "SectionVertexTemplate");
                    }
                    else
                    {
                        // 👉 Bežný stav – použije StateVertexTemplate
                        vc.SetResourceReference(ContentControl.ContentTemplateProperty, "StateVertexTemplate");
                    }
                
                
            }
            graphArea.GenerateGraph(true);

            graphArea.SetVerticesDrag(true, true);
            graphArea.ShowAllEdgesLabels(true);
            //graphArea.ShowAllVerticesLabels(true);

            // 6️⃣ Pridaj GraphArea do hlavného Canvasu
            graphLayer.Children.Add(graphArea);

            //graphArea.Dispatcher.BeginInvoke(() =>
            //{
            //    DrawSections(graphLayer, states);
            //});
            graphArea.RelayoutGraph();

            //try
            //{
            //    graphArea.RelayoutGraph();
            //}
            //catch (ArgumentOutOfRangeException ex)
            //{
            //    Debug.WriteLine("⚠️ GraphX edge layout failed: " + ex.Message);
            //}

            // Zarovnaj do stredu
            double centerX = (Config.DEFAULT_VALUE_CANVAS_X - graphArea.DesiredSize.Width) / 2;
            double centerY = (Config.DEFAULT_VALUE_CANVAS_Y - graphArea.DesiredSize.Height) / 2;
            Canvas.SetLeft(graphArea, centerX);
            Canvas.SetTop(graphArea, centerY);
        }


        private void AddStateRecursive(StateModelData state, StateGraph graph, Dictionary<string, GraphVertex> map)
        {
            GraphVertex vertex;

            if (state.SubStatesData != null && state.SubStatesData.Count > 0)
            {
                vertex = new GraphVertexSection(state); // sekcia
                Debug.WriteLine($"[AddStateRecursive] - section - {state.Name}");
            }
            else
            {
                vertex = new GraphVertex(state); // bežný stav
                Debug.WriteLine($"[AddStateRecursive] - state - {state.Name}");
            }

            graph.AddVertex(vertex);
            map[state.FullIndex] = vertex;

            // Rekurzívne pridaj podstavy
            foreach (var sub in state.SubStatesData)
                AddStateRecursive(sub, graph, map);
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
                Debug.WriteLine($"State: {state.Name} transitions: {grouped.ToList()}");
                foreach (var g in grouped)
                {
                    Debug.WriteLine($"{g}");
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

        private void DrawSections(Canvas canvas, List<StateModelData> states)
        {
            foreach (var state in states)
            {
                if (state.SubStatesData == null || state.SubStatesData.Count == 0)
                    continue;

                var section = new StateSection(state)
                {
                    Width = 400,
                    Height = 300
                };

                // Tu sa rozhodneš, kam ju umiestniť
                Canvas.SetLeft(section, Config.DEFAULT_VALUE_CANVAS_CENTER);
                Canvas.SetTop(section, Config.DEFAULT_VALUE_CANVAS_CENTER);

                canvas.Children.Add(section);

                // Rekurzívne pridaj aj podsekcie
                DrawSections(canvas, state.SubStatesData.ToList());
            }
        }

        private List<StateModelData> GetAllSubStates(StateModelData parent)
        {
            var result = new List<StateModelData>();

            foreach (var sub in parent.SubStatesData)
            {
                result.Add(sub);
                if (sub.SubStatesData != null && sub.SubStatesData.Count > 0)
                    result.AddRange(GetAllSubStates(sub));
            }

            return result;
        }

        private void DrawSection(Canvas canvas, StateModelData section, List<VertexControl> subVertices)
        {
            if (subVertices == null || subVertices.Count == 0)
                return;

            // Vyfiltruj len vertexy, ktoré majú platné pozície
            var validVertices = subVertices
        .Where(v =>
            !double.IsNaN(Canvas.GetLeft(v)) &&
            !double.IsNaN(Canvas.GetTop(v)) &&
            v.ActualWidth > 0 &&
            v.ActualHeight > 0)
        .ToList();
            if (validVertices.Count == 0)
                return;

            double minX = validVertices.Min(v => Canvas.GetLeft(v));
            double minY = validVertices.Min(v => Canvas.GetTop(v));
            double maxX = validVertices.Max(v => Canvas.GetLeft(v) + v.ActualWidth);
            double maxY = validVertices.Max(v => Canvas.GetTop(v) + v.ActualHeight);

            var border = new Border
            {
                Width = (maxX - minX) + 80,
                Height = (maxY - minY) + 80,
                Background = new SolidColorBrush(Color.FromArgb(25, 100, 180, 255)),
                BorderBrush = Brushes.SteelBlue,
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(12),
                SnapsToDevicePixels = true
            };

            var label = new TextBlock
            {
                Text = section.Name.Substring(6).Replace("_", " "),
                Foreground = Brushes.DarkBlue,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, -25, 0, 0)
            };

            var container = new Canvas();
            container.Children.Add(border);
            container.Children.Add(label);

            Canvas.SetLeft(container, minX - 40);
            Canvas.SetTop(container, minY - 40);

            Canvas.SetLeft(label, 10);
            Canvas.SetTop(label, 5);

            if (!canvas.Children.Contains(container))
                canvas.Children.Insert(0, container);
        }


    }
}
