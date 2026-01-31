using MasterMetrology.Controllers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

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
            _processController = new ProcessController(GraphLayer, DiagramCanvas, DiagramBorder, _panZoom);

            _mainView = new MainWindowView(_processController, _panZoom);
            DataContext = _mainView;

            _processController.DataChanged += () =>
            {
                Dispatcher.Invoke(() => _mainView.RefreshFromController());
            };

            _processController.VertexSelected += v =>
            {
                Dispatcher.Invoke(() => _mainView.SelectVertex(v));
            };

            _panZoom.CenterView();
        }

        private void DiagramCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Right) return;

            // pozícia kliknutia vo VIEW (border / viewport)
            var pView = e.GetPosition(DiagramBorder);

            // transform poradie: Scale potom Translate => inverse: (p - translate) / scale
            var s = ZoomTransform.ScaleX; // predpoklad: ScaleY rovnaké
            if (s == 0) s = 1;

            var world = new Point(
                ((pView.X - PanTransform.X) / s) - 25000,
                ((pView.Y - PanTransform.Y) / s) - 25000
            );

            // uložíme do menu, aby si to MenuItem zobral cez CommandParameter
            if (DiagramCanvas.ContextMenu != null)
                DiagramCanvas.ContextMenu.Tag = world;
        }

        private void ShowDebug_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine(StateModelDataDumper.DumpStates(_processController.GetFlatStates()));
        }

        private void Test_Test(object sender, RoutedEventArgs e)
        {
            _processController.test();
        }
    }
}