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
                //Width = 140,
                //Height = 50,
                Background = Brushes.Transparent
            };

            _border = new Border
            {
                //Width = 140,
                //Height = 50,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(200, 34, 68, 102)),
                BorderBrush = Brushes.LightSteelBlue,
                BorderThickness = new Thickness(1)
            };
            

            _label = new TextBlock
            {
                Text = vertex?.State?.Name + "\n" + vertex?.State?.FullIndex ?? "(state)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                //Width = 120
            };

            _rootCanvas.Children.Add(_border);
            _rootCanvas.Children.Add(_label);
            Canvas.SetLeft(_label, 8);
            Canvas.SetTop(_label, 8);

            // pridáme rootCanvas do VisualCollection (visual child controlu)
            _visualChildren.Add(_rootCanvas);

            //this.Width = _label.Width;
            //this.Height = _label.Height;

            //_rootCanvas.Width = this.Width;
            //_rootCanvas.Height = this.Height;

            //_border.Width = this.Width;
            //_border.Height = this.Height;
            
        }

        // public API to change label when data changes
        public void UpdateLabel(string text)
        {
            _label.Text = text;
            InvalidateVisual();
            InvalidateMeasure();
            UpdateLayout(); //#
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
            /*            _rootCanvas.Measure(constraint);
                        // return desired size of rootCanvas
                        var ds = _rootCanvas.DesiredSize;
                        return new Size(ds.Width, ds.Height);*/

            const double DEFAULT_MAX_LABEL_WIDTH = 300.0;
            const double H_PADDING = 12.0;
            const double V_PADDING = 8.0;

            // compute a reasonable max width for the label: if unconstrained, use our default
            double maxLabelWidth = double.IsInfinity(constraint.Width) ? DEFAULT_MAX_LABEL_WIDTH : Math.Min(DEFAULT_MAX_LABEL_WIDTH, constraint.Width);

            // measure label with that max width and unlimited height
            _label.Measure(new Size(maxLabelWidth - (H_PADDING * 2), double.PositiveInfinity));
            Size labelDesired = _label.DesiredSize;

            // final desired size = label + border padding + border thickness
            double finalWidth = labelDesired.Width + (H_PADDING * 2) + _border.BorderThickness.Left + _border.BorderThickness.Right;
            double finalHeight = labelDesired.Height + (V_PADDING * 2) + _border.BorderThickness.Top + _border.BorderThickness.Bottom;

            // ensure minimum sizes (optional)
            finalWidth = Math.Max(finalWidth, 40);
            finalHeight = Math.Max(finalHeight, 24);

            // set sizes on canvas and border so ArrangeOverride has concrete values
            _rootCanvas.Width = finalWidth;
            _rootCanvas.Height = finalHeight;

            _border.Width = finalWidth;
            _border.Height = finalHeight;

            // let the rootCanvas measure itself (it will measure border)
            _rootCanvas.Measure(new Size(finalWidth, finalHeight));

            return new Size(finalWidth, finalHeight);

        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootCanvas.Arrange(new Rect(0, 0, _rootCanvas.Width, _rootCanvas.Height));
            return new Size(_rootCanvas.Width, _rootCanvas.Height);
        }


    }
}
