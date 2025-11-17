using MasterMetrology.Controllers;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Xml.Linq;

namespace MasterMetrology
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private PanAndZoomController _panZoom;
        private ProcessController _processController;
        private GraphVertex _selectedVertex;

        private ICollectionView _transitionsView;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _panZoom = new PanAndZoomController(DiagramBorder, ZoomTransform, PanTransform, Config.DEFAULT_VALUE_CANVAS_X);
            _processController = new ProcessController(GraphLayer);
            _processController.VertexSelected += OnVertexSelected;
            _panZoom.CenterView();
        }

        private void OnVertexSelected(GraphVertex v)
        {
            Dispatcher.Invoke(() =>
            {
                _selectedVertex = v;
                if (v?.State != null)
                {
                    TxtName.Text = v.State.Name ?? "";
                    TxtFullIndex.Text = v.State.FullIndex ?? "";
                    TxtOutput.Text = v.State.Output ?? "";
                    TxtId.Text = v.State.Index ?? "";

                    if (v.State.TransitionsData.Count > 0)
                    {
                        _transitionsView.Filter = o =>
                        {
                            if (o is TransitionViewModel vm) return vm.FromFullIndex == v.State.FullIndex;
                            return false;
                        };
                    }
                }
                else
                {
                    TxtName.Text = "";
                    TxtFullIndex.Text = "";
                    TxtOutput.Text = "";
                    TxtId.Text = "";
                    _transitionsView.Filter = null;
                    
                }
                _transitionsView.Refresh();
            });
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _panZoom.CenterView();
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

                // nakonfiguruj sidepanel:
                _transitionsView = CollectionViewSource.GetDefaultView(_processController.AllTransitions);
                LstTransitions.ItemsSource = _transitionsView;

                // naplň combo box s možnými cieľmi (FullIndex)
                var flat = _processController.GetFlatStates();
                // zobrazíme user-friendly text a použijeme FullIndex ako Value
                CmbTransitionTarget.ItemsSource = flat.Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s.FullIndex }).ToList();
                CmbTransitionTarget.DisplayMemberPath = "Display";
                CmbTransitionTarget.SelectedValuePath = "Value";
                if (CmbTransitionTarget.Items.Count > 0) CmbTransitionTarget.SelectedIndex = 0;


                Debug.WriteLine(filepath);
            }
        }

        private void BtnRemoveTransition_Click(object sender, RoutedEventArgs e)
        {
            if (LstTransitions.SelectedItem is TransitionViewModel vm)
            {
                var r = MessageBox.Show($"Delete transition {vm.DisplayText}?", "Delete", MessageBoxButton.YesNo);
                if (r == MessageBoxResult.Yes)
                    _processController.DeleteTransition(vm);
            }
        }

        private void BtnAddTransition_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVertex == null)
            {
                MessageBox.Show("Select a source state (vertex) first.");
                return;
            }
            var from = _selectedVertex.State?.FullIndex;
            var input = TxtNewTransitionInput.Text?.Trim();
            var target = (string)CmbTransitionTarget.SelectedValue;
            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show("Select a target state.");
                return;
            }
            // pridaj cez controller
            _processController.AddTransition(from, input, target);

            // update listbox selection a vyčisti input
            TxtNewTransitionInput.Text = "";
        }

        private void LstTransitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstTransitions.SelectedItem is TransitionViewModel selectedTransition)
            {
                // napríklad zvýrazníš vybraný transition alebo uložíš pre delete
                Debug.WriteLine($"Selected Transition: {selectedTransition.DisplayText}");
            }
        }
    }
}