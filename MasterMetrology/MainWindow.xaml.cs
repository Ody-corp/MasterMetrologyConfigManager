using MasterMetrology.Controllers;
using System.Diagnostics;
using System.Windows;

namespace MasterMetrology
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private PanAndZoomController _panZoom;
        private ProcessController _processController;
        private MainWindowView _mainView;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panZoom = new PanAndZoomController(DiagramBorder, ZoomTransform, PanTransform, Config.DEFAULT_VALUE_CANVAS_X);
            _processController = new ProcessController(GraphLayer);

            _mainView = new MainWindowView(_processController, _panZoom);
            DataContext = _mainView;

            _processController.VertexSelected += v =>
            {
                Dispatcher.Invoke(() => _mainView.SelectVertex(v));
            };

            _panZoom.CenterView();
        }

        private void ShowDebug_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine(StateModelDataDumper.DumpStates(_processController.GetFlatStates()));
        }
    }
}