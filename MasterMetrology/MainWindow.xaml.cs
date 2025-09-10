using MasterMetrology.Controllers;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MasterMetrology
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private PanAndZoomController _panZoom;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panZoom = new PanAndZoomController(DiagramBorder, ZoomTransform, PanTransform, 50000);
            _panZoom.CenterView();
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _panZoom.CenterView();
        }
    }
}