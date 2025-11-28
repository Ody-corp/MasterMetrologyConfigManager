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
        private GraphVertex _selectedVertex;

        private ICollectionView _transitionsView;

        private List<StateModelData> tempListAddingSubStates = new List<StateModelData>();

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

        private void SetLstChild()
        {
            LstChildren.ItemsSource = _selectedVertex.State.SubStatesData.Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s.FullIndex }).ToList();
            LstChildren.DisplayMemberPath = "Display";
            LstChildren.SelectedValuePath = "Value";
        }
        private void SetCmbChild()
        {
            var flat = _processController.GetFlatStates();
            if (flat == null) { CmbChild.ItemsSource = null; return; }

            var selectedFull = _selectedVertex?.State?.FullIndex; // uprav podľa tvojho selected objektu
            if (string.IsNullOrEmpty(selectedFull))
            {
                // ak nič nie je vybraté — zobrazíme len top-level položky (bez rodiča)
                var top = flat.Where(s => string.IsNullOrEmpty(GetParentFullIndex(s.FullIndex)))
                              .Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s.FullIndex })
                              .ToList();
                CmbChild.ItemsSource = top;
                CmbChild.DisplayMemberPath = "Display";
                CmbChild.SelectedValuePath = "Value";
                return;
            }

            var selParent = GetParentFullIndex(selectedFull);

            var options = flat.Where(s =>
            {
                // exclude self
                if (s.FullIndex == selectedFull) return false;

                // same parent => allowed (siblings)
                var p = GetParentFullIndex(s.FullIndex);
                return string.Equals(p, selParent, StringComparison.Ordinal);
            })
                .Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s.FullIndex })
                .ToList();

            CmbChild.ItemsSource = options;
            CmbChild.DisplayMemberPath = "Display";
            CmbChild.SelectedValuePath = "Value";
        }

        private string GetParentFullIndex(string fullIndex)
        {
            if (string.IsNullOrEmpty(fullIndex)) return string.Empty;
            var i = fullIndex.LastIndexOf('.');
            return i <= 0 ? string.Empty : fullIndex.Substring(0, i);
        }
        private bool IsDescendant(StateModelData ancestor, StateModelData possibleDescendant)
        {
            if (ancestor?.SubStatesData == null || ancestor.SubStatesData.Count == 0) return false;
            foreach (var child in ancestor.SubStatesData)
            {
                if (child.FullIndex == possibleDescendant.FullIndex) return true;
                if (IsDescendant(child, possibleDescendant)) return true;
            }
            return false;
        }
        private void SetCmbParent()
        {
            var flat = _processController.GetFlatStates(); // List<StateModelData>

            // filter kandidátov: vylúč samého seba a svojich potomkov (aby nevznikol cyklus)
            var candidates = flat
                .Where(s => s.FullIndex != _selectedVertex.State.FullIndex)      // nie sám seba
                .Where(s => !IsDescendant(_selectedVertex.State, s))            // nie svoj potomok
                .OrderBy(s => s.FullIndex)
                .ToList();

            // zložíme ItemsSource: prvý item = (none)
            var items = new List<object> { new { Display = "(none)", Value = "" } };
            items.AddRange(candidates.Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s.FullIndex }));

            CmbParent.ItemsSource = items;
            CmbParent.SelectedValuePath = "Value";
            CmbParent.DisplayMemberPath = "Display";

            // prednastav SelectedValue podľa aktuálneho parenta (ak existuje)
            // (použi správne meno property pre parent — tu som predpokladal `ParentFullIndex`)
            var parent = _processController.FindParentByFullIndex(_selectedVertex.State.FullIndex);
            CmbParent.SelectedValue = parent == null ? "" : parent.FullIndex;

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
        private void BtnAddChild_Click(object sender, RoutedEventArgs e)
        {
            var fullIndex = CmbChild.SelectedValue as string;

            if (_selectedVertex.State.SubStatesData.Where(s => s.FullIndex == fullIndex).Count() == 1)
            {
                _processController.Remove_RemovePendingChild(fullIndex);
            }
            _processController.Add_AddPendingChild(fullIndex);
            Debug.WriteLine($"Successfully added. Here is a list -> {_processController.GetPendingAdds().ToString}");
        }
        private void BtnRemoveChild_Click(object sender, RoutedEventArgs e)
        {
            var fullIndex = CmbChild.SelectedValue as string;

            if (_selectedVertex.State.SubStatesData.Where(s => s.FullIndex == fullIndex).Count() == 1)
            {
                _processController.Add_RemovePendingChild(fullIndex);
                //_processController.Remove_AddPendingChild(fullIndex);
            }
            else
            {
                _processController.Remove_AddPendingChild(fullIndex);
            }
        }
        private void BtnApplyEdit_Click(object sender, RoutedEventArgs e)
        {

        }

        // ---------------------------------------- //
        //      SECTION FOR -> SelectionChanged     //
        // ---------------------------------------- //
        private void LstTransitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstTransitions.SelectedItem is TransitionViewModel selectedTransition)
            {
                Debug.WriteLine($"Selected Transition: {selectedTransition.DisplayText}");
            }
        }
        private void CmbParent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void CmbChild_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        
        }
    }
}