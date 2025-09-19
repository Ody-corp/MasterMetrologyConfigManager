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
    internal class StateModelVisual
    {
        public Grid CreateTableData(double x, double y, string name, string index)
        {
            var border = new Border
            {
                Background = Brushes.LightBlue,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2)
            };

            var text = new TextBlock
            {
                Text = name.Substring(6).Replace("_", " ") + "\n" + index,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var grid = new Grid 
            {
                MinWidth = 100,
                Margin = new Thickness(40)
                
            };
            
            grid.Children.Add(border);
            grid.Children.Add(text);

            Canvas.SetLeft(grid, x);
            Canvas.SetTop(grid, y);
            
            return grid;
        }

        public Grid CreateSectionData(double x, double y, string name, string index, double width = 0, double height = 0)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(25)
            };

            var text = new TextBox
            {
                Text = name.Substring(6).Replace("_", " ") + "\n" + index,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Background = Brushes.Transparent
                
            };

            var grid = new Grid
            {
                Background = Brushes.Yellow,
                Margin = new Thickness(40)
            };

            var innerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 40, 0, 0)
            };

            grid.Children.Add(innerPanel);
            grid.Children.Add(border);
            grid.Children.Add(text);

            Canvas.SetLeft(grid, x);
            Canvas.SetTop(grid, y);

            return grid;
        }
    }
}
