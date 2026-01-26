using System.Windows.Media;

namespace MasterMetrology
{
    internal class BaseEdgeVisualState
    {
        // edge
        public Brush Stroke { get; set; } = Brushes.Black;
        public double Thickness { get; set; } = 1.0;
        public double Opacity { get; set; } = 1.0;

        // label
        public Brush LabelForeground { get; set; } = Brushes.Black;
        public Brush LabelBackground { get; set; } = Brushes.White;
        public Brush LabelBorderBrush { get; set; } = Brushes.Black;
        public double LabelOpacity { get; set; } = 1.0;
    }
}
