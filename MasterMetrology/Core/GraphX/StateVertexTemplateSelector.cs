using GraphX.Controls;
using MasterMetrology.Models.Visual;
using MasterMetrology.Controls;
using System.Windows;
using System.Windows.Controls;

namespace MasterMetrology.Core.GraphX
{
    internal class StateVertexTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is GraphVertexSection)
            {
                return (DataTemplate)Application.Current.FindResource("SectionVertexTemplate");
            }

            if (item is GraphVertex)
            {
                return (DataTemplate)Application.Current.FindResource("StateVertexTemplate");
            }

            return base.SelectTemplate(item, container);
        }
    }
}
