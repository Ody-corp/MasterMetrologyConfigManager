// File: Core/GraphX/Controls/SimpleVertexControl.cs
using GraphX.Controls;
using MasterMetrology.Models.Visual;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MasterMetrology.Core.GraphX.Controls
{
    public class SimpleVertexControl : VertexControl
    {
        private readonly VisualCollection _visualChildren;
        private readonly Canvas _rootCanvas;
        private readonly Border _border;
        private readonly TextBlock _label;

        public SimpleVertexControl(GraphVertex vertex) : base(vertex)
        {
            _visualChildren = new VisualCollection(this);

            // uložíme root Canvas do poľa, aby sme naň mohli volať Measure/Arrange
            _rootCanvas = new Canvas
            {
                Width = 140,
                Height = 48,
                Background = Brushes.Transparent
            };

            _border = new Border
            {
                Width = 140,
                Height = 48,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(200, 34, 68, 102)),
                BorderBrush = Brushes.LightSteelBlue,
                BorderThickness = new Thickness(1)
            };

            _label = new TextBlock
            {
                Text = vertex?.State?.Name ?? "(state)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Width = 120
            };

            _rootCanvas.Children.Add(_border);
            _rootCanvas.Children.Add(_label);
            Canvas.SetLeft(_label, 8);
            Canvas.SetTop(_label, 8);

            // pridáme rootCanvas do VisualCollection (visual child controlu)
            _visualChildren.Add(_rootCanvas);

            this.Width = 140;
            this.Height = 48;
        }

        // public API to change label when data changes
        public void UpdateLabel(string text)
        {
            _label.Text = text;
            InvalidateVisual();
            InvalidateMeasure();
        }

        // Override pre VisualCollection
        protected override int VisualChildrenCount => _visualChildren.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _visualChildren.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _visualChildren[index];
        }

        // ---- IMPORTANT: call Measure/Arrange on the stored UIElement (_rootCanvas), not on Visual
        protected override Size MeasureOverride(Size constraint)
        {
            _rootCanvas.Measure(constraint);
            // return desired size of rootCanvas
            var ds = _rootCanvas.DesiredSize;
            return new Size(ds.Width, ds.Height);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootCanvas.Arrange(new Rect(0, 0, _rootCanvas.Width, _rootCanvas.Height));
            return new Size(_rootCanvas.Width, _rootCanvas.Height);
        }
    }
}
