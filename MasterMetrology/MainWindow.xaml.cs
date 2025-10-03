using MasterMetrology.Controllers;
using Microsoft.Win32;
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
        private ProcessController _processController;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panZoom = new PanAndZoomController(DiagramBorder, ZoomTransform, PanTransform, Config.DEFAULT_VALUE_CANVAS_X);
            _processController = new ProcessController(DiagramCanvas);
            _panZoom.CenterView();
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _panZoom.CenterView();
        }

        private void OnClickSpawnObject(object sender, RoutedEventArgs e)
        {
            _processController.SpawnObject();
        }

        private void ExitSoftware(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog 
            { 
                Title = "Chose file",
                Filter = "XML files (*.xml)|*.xml" 
            };
            
            bool? response = ofd.ShowDialog();

            if (response == true)
            {
                string filepath = ofd.FileName;

                _processController.LoadDataXML(filepath);

                System.Diagnostics.Debug.WriteLine(filepath);
            }
        }
    }
}