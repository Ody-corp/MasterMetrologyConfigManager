using MasterMetrology.Controllers;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using Microsoft.Win32;
using System.Collections.Generic;
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

        /*private void OnVertexSelected(GraphVertex v)
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
                            if (o is TransitionViewModel vm) return vm.Transition.FromState.FullIndex == v.State.FullIndex;
                            return false;
                        };
                    }
                    SetCmbChild();
                    SetCmbParent();
                    SetLstChild();
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
            //SetCmbChild();
        }*/
        /*
        private void BtnRemoveTransition_Click(object sender, RoutedEventArgs e)
        {
            if (_mainView.SelectedTransition is not TransitionViewModel tvm) return;
             
            var r = MessageBox.Show($"Delete transition {tvm.DisplayText}?", "Delete", MessageBoxButton.YesNo);
            if (r == MessageBoxResult.Yes)
            {
                _processController.DeleteTransition(tvm);
                _mainView.RefreshTransitionOnly();
            }

        }
        private void BtnAddTransition_Click(object sender, RoutedEventArgs e)
        {
            if (_mainView.SelectedState == null)
            {
                MessageBox.Show("Select a source state (vertex) first.");
                return;
            }
            //var from = _selectedVertex.State?.FullIndex;

            var input = TxtNewTransitionInput.Text?.Trim();
            if (input == null)
            {
                MessageBox.Show("Input cannot be empty.");
                return;
            }

            var target = _mainView.SelectedTransitionTarget;
            if (target == null)
            {
                MessageBox.Show("Select a target state.");
                return;
            }

            // pridaj cez controller
            _processController.AddTransition(from, input, target);

            // update listbox selection a vyčisti input
            TxtNewTransitionInput.Text = "";
            _mainView.RefreshTransitionsOnly();
        }
        private void BtnAddChild_Click(object sender, RoutedEventArgs e)
        {
            if (_mainView.SelectedState == null) return;
            if (_mainView.ChildToAdd == null) return;

            if (_selectedVertex.State.SubStatesData.Any(s => s.SubStatesData.Count(ss => ss.FullIndex == state.FullIndex) == 1))
            {
                _processController.Remove_RemovePendingChild(state);
            }
            _processController.Add_AddPendingChild(state);
            Debug.WriteLine($"Successfully added. Here is a list -> {_processController.GetPendingAdds().ToString}");
        }
        private void BtnRemoveChild_Click(object sender, RoutedEventArgs e)
        {
            var state = LstChildren.SelectedValue as StateModelData;
            Debug.WriteLine($"BUTTON - Remove selected child - {state.Name}");
            if (_selectedVertex.State.SubStatesData.Any(s => s.FullIndex == state.FullIndex))
            {
                Debug.WriteLine($"Add_RemovePendingChild");
                _processController.Add_RemovePendingChild(state);
                //_processController.Remove_AddPendingChild(fullIndex);
            }
            else
            {
                _processController.Remove_AddPendingChild(state);
            }
        }
        private void BtnApplyEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVertex == null || _selectedVertex.State == null)
            {
                MessageBox.Show("Select a state to apply changes.");
                return;
            }

            var oldFull = _selectedVertex.State.FullIndex;
            var newName = TxtName.Text?.Trim() ?? "";
            var newIndex = TxtId.Text?.Trim() ?? "";
            var newOutput = TxtOutput.Text?.Trim() ?? "";

            // 1) pokus o update vlastností (validácia indexu na rovnakej úrovni sa robí tam)
            /*if (!_processController.UpdateStateProperties(oldFull, newName, newIndex, newOutput, out var err))
            {
                MessageBox.Show("Apply failed: " + err, "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 2) nastaviť pending parent podľa vybraného comboboxu (ak tam je)
            var selParent = CmbParent.SelectedValue as string;
            //_processController.SetPendingParent(string.IsNullOrWhiteSpace(selParent) ? null : selParent);
            
            _selectedVertex.State.Name = newName;
            var selectedParent = CmbParent.SelectedValue;
            if (CmbParent.SelectedValue is StateModelData parentState)
            {
                _processController.ApplyPendingChildChanges(_selectedVertex, parentState);
            }
            else
            {
                _processController.ApplyPendingChildChanges(_selectedVertex, null);
            }

            // 3) aplikovať pending child adds/removes (a re-render sa vykoná vnútri tej metódy)

            // 4) refresh UI panel (znovu načíta current selected node state values)
            // after Apply the selected node might have new FullIndex or moved -> refresh selection by finding node
            /*var newFull = _processController.FindStateByFullIndex(newIndex == _selectedVertex.State.Index
                                                                    ? _selectedVertex.State.FullIndex
                                                                    : (_processController.FindParentByFullIndex(oldFull)?.FullIndex == null
                                                                        ? newIndex
                                                                        : $"{_processController.FindParentByFullIndex(oldFull)?.FullIndex}.{newIndex}"));
            
            // safety: simply refresh textboxes from current _selectedVertex.State (if still valid)
            if (_selectedVertex?.State != null)
            {
                TxtName.Text = _selectedVertex.State.Name ?? "";
                TxtFullIndex.Text = _selectedVertex.State.FullIndex ?? "";
                TxtOutput.Text = _selectedVertex.State.Output ?? "";
                TxtId.Text = _selectedVertex.State.Index ?? "";
            }

            // also refresh transitions view
            _transitionsView = CollectionViewSource.GetDefaultView(_processController.AllTransitions);
            LstTransitions.ItemsSource = _transitionsView;
            _transitionsView.Refresh();

            // refresh child lists / comboboxes
            SetCmbChild();
            SetCmbParent();
            SetLstChild();
        }*/
        private void ShowDebug_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine(StateModelDataDumper.DumpStates(_processController.GetFlatStates()));
        }
    }
}