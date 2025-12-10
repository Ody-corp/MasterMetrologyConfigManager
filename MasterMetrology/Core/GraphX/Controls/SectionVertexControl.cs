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

        public SectionVertexControl(GraphVertexSection vertex) : base(vertex)
        {
            _visualChildren = new VisualCollection(this);

            _rootCanvas = new Canvas
            {
                Width = SectionWidth,
                Height = SectionHeight,
                Background = Brushes.Transparent,
                IsHitTestVisible = true
            };


            _border = new Border
            {
                Width = SectionWidth,
                Height = SectionHeight,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                BorderBrush = Brushes.SteelBlue,
                Background = new SolidColorBrush(Color.FromArgb(40, 30, 60, 100))
            };


            _headerRect = new Rectangle
            {
                Width = SectionWidth,
                Height = 34,
                Fill = new SolidColorBrush(Color.FromArgb(160, 30, 60, 120))
            };


            _title = new TextBlock
            {
                Text = vertex?.Section?.Name + "\n" + vertex?.Section?.FullIndex ?? "(section)",
                Foreground = Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                TextAlignment = TextAlignment.Center
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

            this.Width = SectionWidth;
            this.Height = SectionHeight;
        }

        public void SetTitle(string newTitle)
        {
            _title.Text = newTitle;
            InvalidateVisual();
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
    }
}
