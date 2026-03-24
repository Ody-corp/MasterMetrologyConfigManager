using GraphX.Controls;
using MasterMetrology.Models.Visual;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MasterMetrology.Core.GraphX.Controls
{
    public class SimpleVertexControl : VertexControl
    {
        private readonly VisualCollection _visualChildren;
        private readonly Canvas _rootCanvas;
        private readonly Border _border;
        private readonly TextBlock _label;
        private readonly Rectangle _headerRect;

        public SimpleVertexControl(GraphVertex vertex) : base(vertex)
        {
            _visualChildren = new VisualCollection(this);

            var cm = Application.Current.FindResource("rightClickContextMenu") as ContextMenu;
            this.Tag = vertex;
            _rootCanvas = new Canvas
            {
                Background = Brushes.Transparent,
                ContextMenu = cm
            };
            ContextMenuService.SetContextMenu(this, cm);

            _border = new Border
            {
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
                TextAlignment = TextAlignment.Center,

                //Width = 120
            };

            _rootCanvas.Children.Add(_border);
            _rootCanvas.Children.Add(_label);
            Canvas.SetLeft(_label, 8);
            Canvas.SetTop(_label, 8);

            _visualChildren.Add(_rootCanvas);

            Loaded += SimpleVertexControl_Loaded;
        }

        public void UpdateLabel(string text)
        {
            _label.Text = text;
            InvalidateVisual();
            InvalidateMeasure();
            UpdateLayout();
        }

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

        private ContextMenu GetVertexMenu()
            => (ContextMenu)Application.Current.MainWindow.FindResource("rightClickContextMenu");

        private void SimpleVertexControl_Loaded(object sender, RoutedEventArgs e)
        {
            var cm = GetVertexMenu();

            this.ContextMenu = cm;
            ContextMenuService.SetContextMenu(this, cm);

            _rootCanvas.ContextMenu = cm;

            this.AddHandler(UIElement.PreviewMouseRightButtonDownEvent,
                           new System.Windows.Input.MouseButtonEventHandler(OnPreviewRightDown), true);

        }
        private void OnPreviewRightDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Right) return;

            var cm = GetVertexMenu();
            cm.DataContext = Application.Current.MainWindow.DataContext;

            cm.PlacementTarget = this;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint; // otvor pri kurzore
            cm.IsOpen = true;

            e.Handled = true;
        }
    }
}
