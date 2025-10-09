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
        /*public Grid CreateTableData(double x, double y, string name, string index)
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

            var innerGrid = new Grid
            {
                Margin = new Thickness(
                    0, 
                    Config.DEFAULT_VALUE_INNER_PANEL_MARGIN, 
                    0, 
                    0)
            };

            grid.Children.Add(innerGrid);
            grid.Children.Add(border);
            grid.Children.Add(text);

            Canvas.SetLeft(grid, x);
            Canvas.SetTop(grid, y);

            return grid;
        }*/
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
                MinHeight = 60,
                Margin = new Thickness(Config.DEFAULT_VALUE_GRID_MARGIN)
            };

            grid.Children.Add(border);
            grid.Children.Add(text);

            Canvas.SetLeft(grid, x);
            Canvas.SetTop(grid, y);

            return grid;
        }

        // Sekcia je len vizuálny obrys – neobsahuje žiadne deti
        public Grid CreateSectionBorder(string name, string index, Rect bounds)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(Config.DEFAULT_VALUE_BORDER_THICKNESS),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Width = bounds.Width,
                Height = bounds.Height,
            };

            var label = new TextBlock
            {
                Text = $"{name.Substring(6).Replace("_", " ")}\n{index}",
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Pridáme text ako overlay
            var container = new Grid();
            container.Children.Add(border);
            container.Children.Add(label);

            Canvas.SetLeft(container, bounds.X);
            Canvas.SetTop(container, bounds.Y);

            return container;
        }
    }
}
