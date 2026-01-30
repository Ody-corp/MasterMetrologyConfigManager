using GraphX.Controls;
using MasterMetrology.Models.Visual;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MasterMetrology.Core.GraphX.Controls
{
    public class SectionVertexControl : VertexControl
    {
        private readonly VisualCollection _visualChildren;
        private readonly Canvas _rootCanvas;
        private readonly Border _border;
        private readonly TextBlock _title;
        private readonly Rectangle _headerRect;

        public double SectionWidth { get; set; } = 260;
        public double SectionHeight { get; set; } = 180;

        public Canvas RootCanvas => _rootCanvas;

        private readonly GraphVertexSection _section;

        public SectionVertexControl(GraphVertexSection vertex) : base(vertex)
        {
            _visualChildren = new VisualCollection(this);

            var cm = Application.Current.FindResource("rightClickContextMenu") as ContextMenu;

            _section = vertex;
            this.Tag = vertex;
            _rootCanvas = new Canvas
            {
                Width = SectionWidth,
                Height = SectionHeight,
                Background = null,
                IsHitTestVisible = true,
                //ContextMenu = Application.Current.FindResource("rightClickContextMenu") as ContextMenu,
                ContextMenu = cm
            };


            _border = new Border
            {
                Width = SectionWidth,
                Height = SectionHeight,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                BorderBrush = Brushes.SteelBlue,
                Background = new SolidColorBrush(Color.FromArgb(40, 30, 60, 100)),
                IsHitTestVisible = false
            };


            _headerRect = new Rectangle
            {
                Width = SectionWidth,
                Height = 34,
                Fill = new SolidColorBrush(Color.FromArgb(160, 30, 60, 120)),
                IsHitTestVisible = true,
                //ContextMenu = (Application.Current.FindResource("rightClickContextMenu") as ContextMenu),

            };


            _title = new TextBlock
            {
                Text = vertex?.Section?.Name + "\n" + vertex?.Section?.FullIndex ?? "(section)",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };

            _rootCanvas.Children.Add(_border);
            _rootCanvas.Children.Add(_headerRect);
            Canvas.SetLeft(_headerRect, 0);
            Canvas.SetTop(_headerRect, 0);

            _title.Width = SectionWidth - 16;
            Canvas.SetLeft(_title, 8);
            Canvas.SetTop(_title, 6);
            _rootCanvas.Children.Add(_title);

            _visualChildren.Add(_rootCanvas);

            Loaded += SectionVertexControl_Loaded;

            this.Width = SectionWidth;
            this.Height = SectionHeight;
        }

        public void SetSize(double width, double height)
        {
            SectionWidth = width;
            SectionHeight = height;

            _border.Width = width;
            _border.Height = height;
            _headerRect.Width = width;

            _rootCanvas.Width = width;
            _rootCanvas.Height = height;
            this.Width = width;
            this.Height = height;

            InvalidateMeasure();
        }

        protected override int VisualChildrenCount => _visualChildren.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _visualChildren.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _visualChildren[index];
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _rootCanvas.Measure(constraint);
            return _rootCanvas.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootCanvas.Arrange(new Rect(0, 0, _rootCanvas.Width, _rootCanvas.Height));
            return new Size(_rootCanvas.Width, _rootCanvas.Height);
        }
        private ContextMenu GetVertexMenu()
    => (ContextMenu)Application.Current.MainWindow.FindResource("rightClickContextMenu");

        private void SectionVertexControl_Loaded(object sender, RoutedEventArgs e)
        {
            // ak je menu vo Window.Resources:
            var cm = GetVertexMenu();

            // nastav na celý vertex (najistejšie)
            this.ContextMenu = cm;
            ContextMenuService.SetContextMenu(this, cm);
            
            // ak chceš aj na konkrétne vnútorné elementy:
            _rootCanvas.ContextMenu = cm;
            _headerRect.ContextMenu = cm;

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
