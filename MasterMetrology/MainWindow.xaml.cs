using MasterMetrology.Core.UI;
using MasterMetrology.Core.UI.Controllers;
using MasterMetrology.Utils;
using Microsoft.Win32;
using System.ComponentModel;
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
        private KeyboardPanController _keyboardPanController;
        private KeyBindController _keyBindController;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panZoom = new PanAndZoomController(DiagramBorder, ZoomTransform, PanTransform, Config.DEFAULT_VALUE_CANVAS_X);
            _keyboardPanController = new KeyboardPanController(this, _panZoom);
            
            this.Focusable = true;
            this.Focus();
            Keyboard.Focus(this);
            
            _processController = new ProcessController(GraphLayer, DiagramCanvas, DiagramBorder, _panZoom);

            _mainView = new MainWindowView(_processController, _panZoom);
            DataContext = _mainView;

            _keyBindController = new KeyBindController(this, _mainView);

            _processController.DataChanged += () =>
            {
                Dispatcher.Invoke(() => _mainView.RefreshFromController());
            };

            _processController.VertexSelected += v =>
            {
                Dispatcher.Invoke(() => _mainView.SelectVertex(v));
            };

            _processController.WireMonitorChanges();

            _panZoom.CenterView();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (_processController.ProcessDecisionExitWin())
                e.Cancel = true;   
        }

        private void DiagramCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Right) return;

            var pView = e.GetPosition(DiagramBorder);

            var s = ZoomTransform.ScaleX;
            if (s == 0) s = 1;

            var world = new Point(
                ((pView.X - PanTransform.X) / s) - 25000,
                ((pView.Y - PanTransform.Y) / s) - 25000
            );

            if (DiagramCanvas.ContextMenu != null)
                DiagramCanvas.ContextMenu.Tag = world;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(this, this);
        }

        private void ShowDebug_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine(StateModelDataDumper.DumpStates(_processController.GetFlatStates()));
        }
    }
}