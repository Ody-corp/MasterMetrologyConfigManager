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
                BorderThickness = new Thickness(Config.DEFAULT_VALUE_BORDER_THICKNESS)
            };

            var text = new TextBlock
            {
                Text = name.Substring(6).Replace("_", " ") + "\n" + index,
                Margin = new Thickness(Config.DEFAULT_VALUE_TEXT_MARGIN),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var grid = new Grid 
            {
                MinWidth = 100,
                Margin = new Thickness(Config.DEFAULT_VALUE_GRID_MARGIN)
                
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
                BorderThickness = new Thickness(Config.DEFAULT_VALUE_BORDER_THICKNESS),
                Padding = new Thickness(Config.DEFAULT_VALUE_BORDER_PADDING)
            };

            var text = new TextBox
            {
                Text = name.Substring(6).Replace("_", " ") + "\n" + index,
                Margin = new Thickness(Config.DEFAULT_VALUE_TEXT_MARGIN),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                Background = Brushes.Transparent
                
            };

            var grid = new Grid
            {
                Background = Brushes.Yellow,
                Margin = new Thickness(Config.DEFAULT_VALUE_GRID_MARGIN)
            };

            var innerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(
                    0, 
                    Config.DEFAULT_VALUE_INNER_PANEL_MARGIN, 
                    0, 
                    0)
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
