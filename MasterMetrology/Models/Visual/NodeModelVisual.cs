using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Shapes;

namespace MasterMetrology.Models.Visual
{
    internal class NodeModelVisual
    {
        public Grid CreateTableData(double x, double y, string name)
        {
            var rect = new Rectangle
            {
                Width = 120,
                Height = 60,
                Fill = Brushes.LightBlue,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };

            var text = new TextBlock
            {
                Text = name,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var grid = new Grid { Width = 120, Height = 60 };
            grid.Children.Add(rect);
            grid.Children.Add(text);

            Canvas.SetLeft(grid, x);
            Canvas.SetTop(grid, y);
            
            return grid;
        }
    }
}
