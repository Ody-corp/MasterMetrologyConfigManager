using GraphX.PCL.Common.Models;
using MasterMetrology.Models.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MasterMetrology.Controls
{
    public partial class StateSection : UserControl
    {
        public static readonly DependencyProperty SectionDataProperty =
            DependencyProperty.Register(
                nameof(SectionData),
                typeof(StateModelData),
                typeof(StateSection),
                new PropertyMetadata(null, OnSectionDataChanged));

        public StateModelData SectionData
        {
            get => (StateModelData)GetValue(SectionDataProperty);
            set => SetValue(SectionDataProperty, value);
        }

        private static void OnSectionDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StateSection section && e.NewValue is StateModelData data)
            {
                section.UpdateSection(data);
            }
        }
        private void UpdateSection(StateModelData section)
        {
            SectionTitle.Text = FormatName(section.Name + "\n" + section.FullIndex);
            InnerCanvas.Children.Clear();
            LayoutSubStates(section.SubStatesData);
        }
        private readonly Dictionary<string, FrameworkElement> _subStateVisuals = new();

        public StateSection()
        {
            InitializeComponent();
        }
        public StateSection(StateModelData section)
        {
            InitializeComponent();
            SectionData = section;
            SectionTitle.Text = FormatName(section.Name + "\n" + section.FullIndex);

            // Dočasne staticky rozmiestnime vnútorné stavy
            LayoutSubStates(section.SubStatesData);

            Debug.WriteLine($"🧩 StateSection vytvorený: {section.Name}");
        }

        private string FormatName(string rawName)
        {
            return rawName.StartsWith("STATE_")
                ? rawName.Substring(6).Replace("_", " ")
                : rawName.Replace("_", " ");
        }

        private void LayoutSubStates(IEnumerable<StateModelData> subStates)
        {
            double x = 30;
            double y = 40;
            double stepY = 60;

            foreach (var sub in subStates)
            {
                var rect = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(60, 180, 220, 255)),
                    BorderBrush = Brushes.LightSteelBlue,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Child = new TextBlock
                    {
                        Text = FormatName(sub.Name + "\n" + sub.FullIndex),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.White
                    }
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                InnerCanvas.Children.Add(rect);
                _subStateVisuals[sub.FullIndex] = rect;

                y += stepY;

                // Rekurzívne vnorené sekcie
                if (sub.SubStatesData.Count > 0)
                {
                    var nested = new StateSection(sub)
                    {
                        Width = 220,
                        Height = 150
                    };
                    Canvas.SetLeft(nested, x + 180);
                    Canvas.SetTop(nested, y - 20);
                    InnerCanvas.Children.Add(nested);
                }
            }
        }
    }
}
