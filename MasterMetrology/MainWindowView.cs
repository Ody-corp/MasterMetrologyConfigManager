using MasterMetrology.Controllers;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace MasterMetrology
{
    internal class MainWindowView : INotifyPropertyChanged
    {
        private readonly ProcessController _processController;
        private readonly PanAndZoomController _panAndZoomController;

        private HashSet<StateModelData> _originalChildren = new();
        private StateModelData? _originalParentModel;

        // map model -> VM (jedna VM na jeden model)
        //private readonly Dictionary<StateModelData, StateViewModel> _map = new();


        public MainWindowView(ProcessController processController, PanAndZoomController panAndZoomController)
        {
            _processController = processController;
            _panAndZoomController = panAndZoomController;

            // Transitions view (filtrovaný pohľad)
            TransitionsView = CollectionViewSource.GetDefaultView(_processController.AllTransitions);

            // Commands
            ApplyCommand = new RelayCommand(Apply, () => SelectedState != null && SelectedVertex != null);
            AddChildCommand = new RelayCommand(AddChild, () => SelectedState != null && ChildToAdd != null);
            RemoveChildCommand = new RelayCommand(RemoveChild, () => SelectedState != null && SelectedChild != null);

            AddTransitionCommand = new RelayCommand(AddTransition, () => SelectedState != null && SelectedTransitionTarget != null && !string.IsNullOrWhiteSpace(NewTransitionInput));
            RemoveTransitionCommand = new RelayCommand(RemoveTransition, () => SelectedTransition != null);

            ExitAppCommand = new RelayCommand(ExitApp);
            ImportFileCommand = new RelayCommand(ImportFile);
            CenterViewCommand = new RelayCommand(CenterView);

            RefreshFromController();
        }

        // --------- SELECTION ----------
        public GraphVertex? SelectedVertex { get; private set; }

        private StateViewModel? _selectedState;
        public StateViewModel? SelectedState
        {
            get => _selectedState;
            set
            {
                if (_selectedState == value) return;
                _selectedState = value;
                OnPropertyChanged();

                // keď zmeníš selected, naplň drafty z modelu (buffer)
                LoadDraftFromSelected();

                RefreshCandidates();
                RefreshTransitionsFilter();

                RaiseAllCanExecute();
            }
        }

        // --------- EDIT BUFFER (draft) ----------
        private string _draftName = "";
        public string DraftName
        {
            get => _draftName;
            set { _draftName = value; OnPropertyChanged(); }
        }

        private string _draftIndex = "";
        public string DraftIndex
        {
            get => _draftIndex;
            set { _draftIndex = value; OnPropertyChanged(); }
        }

        private string _draftOutput = "";
        public string DraftOutput
        {
            get => _draftOutput;
            set { _draftOutput = value; OnPropertyChanged(); }
        }

        private StateViewModel? _draftParent;
        public StateViewModel? DraftParent
        {
            get => _draftParent;
            set 
            { 
                if (_draftParent == value) return;
                _draftParent = value; 
                OnPropertyChanged(); 

                RefreshCandidates();
                UpdateDraftMarkers();
                RaiseAllCanExecute(); 
            }
        }

        public ObservableCollection<StateViewModel> DraftChildren { get; } = new();

        private StateViewModel? _selectedChild;
        public StateViewModel? SelectedChild
        {
            get => _selectedChild;
            set { _selectedChild = value; OnPropertyChanged(); RaiseAllCanExecute(); }
        }

        private StateViewModel? _childToAdd;
        public StateViewModel? ChildToAdd
        {
            get => _childToAdd;
            set { _childToAdd = value; OnPropertyChanged(); RaiseAllCanExecute(); }
        }

        // --------- STATES LISTS ----------
        public ObservableCollection<StateViewModel> FlatStates { get; } = new();

        private ObservableCollection<StateViewModel> _parentCandidates = new();
        public ObservableCollection<StateViewModel> ParentCandidates
        {
            get => _parentCandidates;
            private set { _parentCandidates = value; OnPropertyChanged(); }
        }

        private ObservableCollection<StateViewModel> _childCandidates = new();
        public ObservableCollection<StateViewModel> ChildCandidates
        {
            get => _childCandidates;
            private set { _childCandidates = value; OnPropertyChanged(); }
        }

        // --------- TRANSITIONS ----------
        public ICollectionView TransitionsView { get; }

        private TransitionViewModel? _selectedTransition;
        public TransitionViewModel? SelectedTransition
        {
            get => _selectedTransition;
            set 
            { 
                _selectedTransition = value; 
                OnPropertyChanged(); 
                RaiseAllCanExecute(); 
            }
        }

        private string _newTransitionInput = "";
        public string NewTransitionInput
        {
            get => _newTransitionInput;
            set 
            { 
                _newTransitionInput = value; 
                OnPropertyChanged(); 
                RaiseAllCanExecute(); 
            }
        }

        private StateViewModel? _selectedTransitionTarget;
        public StateViewModel? SelectedTransitionTarget
        {
            get => _selectedTransitionTarget;
            set 
            { 
                _selectedTransitionTarget = value; 
                OnPropertyChanged(); 
                RaiseAllCanExecute(); 
            }
        }

        // --------- COMMANDS ----------
        public RelayCommand ApplyCommand { get; }
        public RelayCommand AddChildCommand { get; }
        public RelayCommand RemoveChildCommand { get; }
        public RelayCommand AddTransitionCommand { get; }
        public RelayCommand RemoveTransitionCommand { get; }
        public RelayCommand ExitAppCommand { get; }
        public RelayCommand ImportFileCommand { get; }
        public RelayCommand CenterViewCommand { get; }

        // --------- PUBLIC API CALLED FROM WINDOW ----------
        public void SelectVertex(GraphVertex v)
        {
            SelectedVertex = v;

            if (v?.State == null)
            {
                SelectedState = null;
                return;
            }

            if (_processController.modelToViewModel.TryGetValue(v.State, out var svm))
            {
                SelectedState = svm;
            }
            else
            {
                SelectedState = FlatStates.FirstOrDefault(s => s.FullIndex == v.State.FullIndex);
            }
        }

        public void RefreshFromController()
        {
            var flatModels = _processController.GetFlatStates()
                .OrderBy(s => s.FullIndex)
                .ToList();

            _processController.modelToViewModel.Clear();
            FlatStates.Clear();

            foreach (var m in flatModels)
            {
                var vm = new StateViewModel(m);
                _processController.modelToViewModel[m] = vm;
                FlatStates.Add(vm);
            }

            // prepojenie parent/substates
            foreach (var m in flatModels)
            {
                var vm = _processController.modelToViewModel[m];

                vm.SubStates.Clear();
                foreach (var child in m.SubStatesData)
                {
                    if (_processController.modelToViewModel.TryGetValue(child, out var childVm))
                        vm.SubStates.Add(childVm);
                }

                vm.Parent = (m.Parent != null && _processController.modelToViewModel.TryGetValue(m.Parent, out var pvm)) ? pvm : null;
            }

            // ak niečo bolo vybrané, skús zachovať selection
            if (SelectedState != null && _processController.modelToViewModel.TryGetValue(SelectedState.StateModel, out var newSel))
                SelectedState = newSel;

            RefreshCandidates();
            RefreshTransitionsFilter();
            RaiseAllCanExecute();
        }

        // --------- INTERNAL HELPERS ----------
        private void LoadDraftFromSelected()
        {
            DraftChildren.Clear();
            ChildToAdd = null;
            SelectedChild = null;

            foreach (var vm in FlatStates)
            {
                vm.IsDraftAdded = false;
                vm.IsDraftRemoved = false;
            }

            if (SelectedState == null)
            {
                DraftName = "";
                DraftIndex = "";
                DraftOutput = "";
                DraftParent = null;
                _originalChildren = new HashSet<StateModelData>();
                _originalParentModel = null;
                return;
            }

            _originalChildren = new HashSet<StateModelData>(SelectedState.StateModel.SubStatesData);
            _originalParentModel = SelectedState.StateModel.Parent;

            DraftName = SelectedState.Name ?? "";
            DraftIndex = SelectedState.Index ?? "";
            DraftOutput = SelectedState.Output ?? "";
            DraftParent = SelectedState.Parent;

            foreach (var ch in SelectedState.SubStates)
                DraftChildren.Add(ch);

            UpdateDraftMarkers();
        }
        private void UpdateDraftMarkers()
        {
            //foreach (var vm in FlatStates)
            //{
            //    vm.IsDraftAdded = false;
            //    vm.IsDraftRemoved = false;
            //}

            if (SelectedState == null) return;

            var draftModels = new HashSet<StateModelData>(DraftChildren.Select(c => c.StateModel));

            var added = draftModels.Except(_originalChildren).ToList();
            var removed = _originalChildren.Except(draftModels).ToList();

            foreach (var m in added)
            {
                if (_processController.modelToViewModel.TryGetValue(m, out var vm))
                    vm.IsDraftAdded = true;
            }

            foreach (var m in removed)
            {
                if (_processController.modelToViewModel.TryGetValue(m, out var vm))
                    vm.IsDraftRemoved = true;
            }
        }

        private void RefreshCandidates()
        {
            if (SelectedState == null)
            {
                ParentCandidates = new ObservableCollection<StateViewModel>(FlatStates);
                ChildCandidates = new ObservableCollection<StateViewModel>(FlatStates);
                return;
            }

            var draftChildModels = new HashSet<StateModelData>(DraftChildren.Select(c => c.StateModel));
            var draftParentModel = DraftParent?.StateModel;

            // Parent candidates: všetko okrem self a potomkov (anti-cycle)
            var parentList = FlatStates
                .Where(vm => vm != SelectedState)
                .Where(vm => !IsDescendant(SelectedState.StateModel, vm.StateModel))
                .Where(vm => !draftChildModels.Contains(vm.StateModel))
                .ToList();

            ParentCandidates = new ObservableCollection<StateViewModel>(parentList);

            // Child candidates: všetko okrem self + predkov + potomkov (anti-cycle)
            var childList = FlatStates
                .Where(vm => vm != SelectedState)
                .Where(vm => !IsDescendant(SelectedState.StateModel, vm.StateModel))
                .Where(vm => !IsDescendant(vm.StateModel, SelectedState.StateModel))
                .Where(vm => draftParentModel == null || vm.StateModel != draftParentModel)
                .Where(vm => !draftChildModels.Contains(vm.StateModel))
                .ToList();

            ChildCandidates = new ObservableCollection<StateViewModel>(childList);
        }

        private void RefreshTransitionsFilter()
        {
            if (SelectedState == null)
            {
                TransitionsView.Filter = null;
            }
            else
            {
                var full = SelectedState.FullIndex;
                TransitionsView.Filter = o =>
                {
                    if (o is TransitionViewModel tvm)
                        return tvm.FromState.FullIndex == full;
                    return false;
                };
            }
            TransitionsView.Refresh();
            OnPropertyChanged(nameof(TransitionsView));
        }

        private static bool IsDescendant(StateModelData ancestor, StateModelData possibleDescendant)
        {
            if (ancestor?.SubStatesData == null || ancestor.SubStatesData.Count == 0) return false;
            foreach (var child in ancestor.SubStatesData)
            {
                if (ReferenceEquals(child, possibleDescendant)) return true;
                if (IsDescendant(child, possibleDescendant)) return true;
            }
            return false;
        }

        private void RaiseAllCanExecute()
        {
            ApplyCommand.RaiseCanExecuteChanged();
            AddChildCommand.RaiseCanExecuteChanged();
            RemoveChildCommand.RaiseCanExecuteChanged();
            AddTransitionCommand.RaiseCanExecuteChanged();
            RemoveTransitionCommand.RaiseCanExecuteChanged();
        }

        // --------- COMMAND IMPLEMENTATIONS ----------
        private void AddChild()
        {
            if (SelectedState == null || ChildToAdd == null) return;

            if (!DraftChildren.Any(x => x.StateModel == ChildToAdd.StateModel))
                DraftChildren.Add(ChildToAdd);

            ChildToAdd = null;

            RefreshCandidates();
            UpdateDraftMarkers();
            RaiseAllCanExecute();
        }

        private void RemoveChild()
        {
            if (SelectedChild == null) return;
            DraftChildren.Remove(SelectedChild);

            SelectedChild = null;

            RefreshCandidates();
            UpdateDraftMarkers();
            RaiseAllCanExecute();
        }

        private void AddTransition()
        {
            if (SelectedState == null || SelectedTransitionTarget == null) return;

            var input = NewTransitionInput.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            // transitions chceme hneď -> voláme controller
            _processController.AddTransition(SelectedState, input, SelectedTransitionTarget);

            NewTransitionInput = "";
            RefreshTransitionsFilter();
        }

        private void RemoveTransition()
        {
            if (SelectedTransition == null) return;
            _processController.DeleteTransition(SelectedTransition);
            SelectedTransition = null;
            RefreshTransitionsFilter();
        }

        private void Apply()
        {
            if (SelectedState == null || SelectedVertex == null) return;

            // 1) Name/Index/Output -> commit do modelu až tu
            SelectedState.Name = DraftName;
            SelectedState.Output = DraftOutput;

            // Index meníš až tu (pozor: FullIndex sa má prerátať v tvojej logike)
            // Ak máš UpdateStateProperties, je lepšie ho použiť – ale keď ho teraz nepoužívaš,
            // necháme to cez tvoj existujúci ApplyPendingChildChanges (ten reindexuje/move rieši).
            SelectedState.Index = DraftIndex;

            // 2) priprav pending add/remove pre children podľa rozdielu model vs draft
            // vyčisti pending v controlleri
            _processController.ClearPendingChildChanges();

            var currentChildren = SelectedState.StateModel.SubStatesData.ToList();
            var draftModels = DraftChildren.Select(c => c.StateModel).ToList();

            var toRemove = currentChildren.Where(m => !draftModels.Contains(m)).ToList();
            var toAdd = draftModels.Where(m => !currentChildren.Contains(m)).ToList();

            foreach (var rm in toRemove) _processController.Add_RemovePendingChild(rm);
            foreach (var ad in toAdd) _processController.Add_AddPendingChild(ad);

            // 3) Parent commit až tu
            var parentModel = DraftParent?.StateModel; // null => top-level
            _processController.ApplyPendingChildChanges(SelectedVertex, parentModel);

            // 4) po apply sa modely zmenili (fullindexy/parenti) -> rebuild VM map a drafty
            RefreshFromController();
        }

        private void ExitApp()
        {
            Application.Current.Shutdown();
        }

        private void ImportFile()
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
                RefreshFromController();
            }
        }

        private void CenterView()
        {
            _panAndZoomController.CenterView();
        }

        // --------- INotifyPropertyChanged ----------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
