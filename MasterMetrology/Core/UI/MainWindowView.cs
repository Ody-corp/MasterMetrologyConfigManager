using GraphX.Controls;
using MasterMetrology.Core.UI.Controllers;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using MasterMetrology.Utils;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MasterMetrology.Core.UI
{
    internal class MainWindowView : INotifyPropertyChanged
    {
        private readonly ProcessController _processController;
        private readonly PanAndZoomController _panAndZoomController;

        private HashSet<StateModelData> _originalChildren = new();
        private StateModelData? _originalParentModel;

        private bool _statePanelDataChange;
        public bool StatePanelDataChange
        {
            get => _statePanelDataChange;
            set
            {
                if (_statePanelDataChange == value) return;
                _statePanelDataChange = value;
            }
        }

        private bool _supressStatePanelDirty;
        private void MarkStatePanelDirty()
        {
            if (_supressStatePanelDirty) return;
            StatePanelDataChange = true;
            OnPropertyChanged(nameof(StatePanelDataChange));
        }


        public MainWindowView(ProcessController processController, PanAndZoomController panAndZoomController)
        {
            _processController = processController;
            _panAndZoomController = panAndZoomController;

            // Transitions view (filtrovaný pohľad)
            TransitionsView = CollectionViewSource.GetDefaultView(_processController.AllTransitions);

            // Commands
            ApplyCommand = new RelayCommand(() => { Apply(); _processController.MarkDirty(); StatePanelDataChange = false; OnPropertyChanged(nameof(StatePanelDataChange)); }, () => SelectedState != null && SelectedVertex != null);
            AddChildCommand = new RelayCommand(() => { AddChild(); _processController.MarkDirty(); StatePanelDataChange = true; OnPropertyChanged(nameof(StatePanelDataChange)); }, () => SelectedState != null && ChildToAdd != null);
            RemoveChildCommand = new RelayCommand(() => { RemoveChild(); _processController.MarkDirty(); StatePanelDataChange = true; OnPropertyChanged(nameof(StatePanelDataChange)); }, () => SelectedState != null && SelectedChild != null);

            AddTransitionCommand = new RelayCommand(() => { AddTransition(); _processController.MarkDirty(); }, () => SelectedState != null && SelectedTransitionTarget != null && !string.IsNullOrWhiteSpace(NewTransitionInput));
            RemoveTransitionCommand = new RelayCommand(() => { RemoveTransition(); _processController.MarkDirty(); }, () => SelectedTransition != null);

            ExitAppCommand = new RelayCommand(ExitApp);
            ImportFileCommand = new RelayCommand(ImportFile);
            CenterViewCommand = new RelayCommand(CenterView);

            AddStateToRootCommand = new RelayCommand(() => { _processController.CreateNewRootStateAtViewCenter(); _processController.MarkDirty(); });
            AddStateToRootAtPointCommand = new RelayCommand(p => { if (p is Point pt) _processController.CreateNewRootStateAt(pt); _processController.MarkDirty(); }, p => p is Point);
            AddStateAsSubStateCommand = new RelayCommand(() => { _processController.CreateNewSubState(SelectedVertex!.State); _processController.MarkDirty(); }, () => SelectedState != null);
            AddStateAsSubStateCMCommand = new RelayCommand(p => { if (p is GraphVertex gv && gv.State != null) _processController.CreateNewSubState(gv.State); _processController.MarkDirty(); }, p => p is GraphVertex gv && gv.State != null);

            DeleteWholeSelectedStateCommand = new RelayCommand(() => { _processController.DeleteWholeState(SelectedVertex!.State); _processController.MarkDirty(); }, () => SelectedState != null);
            DeleteWholeStateCommand = new RelayCommand(p => { if (p is GraphVertex gv && gv.State != null) _processController.DeleteWholeState(gv.State); _processController.MarkDirty(); }, p => p is GraphVertex gv && gv.State != null);

            DeleteSingleSelectedStateCommand = new RelayCommand(() => { _processController.DeleteSingleState(SelectedVertex!.State); _processController.MarkDirty(); }, () => SelectedVertex?.State != null);
            DeleteSingleStateCommand = new RelayCommand(p => { if (p is GraphVertex gv && gv.State != null) _processController.DeleteSingleState(gv.State); _processController.MarkDirty(); }, p => p is GraphVertex gv && gv.State != null);

            SaveCommand = new RelayCommand(Save, () => _processController.CanSave);
            SaveAsCommand = new RelayCommand(SaveAs, () => _processController.CanSaveAs);

            AddInputCommand = new RelayCommand(() => { AddInput(); _processController.MarkDirty(); });
            RemoveInputCommand = new RelayCommand(() => { RemoveInput(); _processController.MarkDirty(); }, () => SelectedInput != null);

            AddOutputCommand = new RelayCommand(() => { AddOutput(); _processController.MarkDirty(); });
            RemoveOutputCommand = new RelayCommand(() => { RemoveOutput(); _processController.MarkDirty(); }, () => SelectedOutput != null);

            _processController.DataChanged += () =>
            {
                SaveCommand.RaiseCanExecuteChanged();
                SaveAsCommand.RaiseCanExecuteChanged();
            };

            RefreshFromController();
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
        public RelayCommand AddStateToRootCommand { get; }
        public RelayCommand AddStateToRootAtPointCommand { get; }
        public RelayCommand AddStateAsSubStateCommand { get; }
        public RelayCommand AddStateAsSubStateCMCommand { get; }
        public RelayCommand DeleteWholeSelectedStateCommand { get; }
        public RelayCommand DeleteWholeStateCommand { get; }
        public RelayCommand DeleteSingleSelectedStateCommand { get; }
        public RelayCommand DeleteSingleStateCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand SaveAsCommand { get; }
        public RelayCommand AddInputCommand { get; }
        public RelayCommand RemoveInputCommand { get; }
        public RelayCommand AddOutputCommand { get; }
        public RelayCommand RemoveOutputCommand { get; }

        // --------- SELECTION ----------
        public GraphVertex? SelectedVertex { get; private set; }
        public bool IsSelectedVertex => SelectedVertex == null;
        public bool IsSelectedVertexAndSection => SelectedVertex == null || (SelectedState != null && SelectedState.IsSection);
        private StateViewModel? _selectedState;
        public StateViewModel? SelectedState
        {
            get => _selectedState;
            set
            {
                if (_selectedState == value) return;

                

                _selectedState = value;
                _processController.SetSelectedVertex(SelectedVertex);

                OnPropertyChanged();

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
            set
            {
                MarkStatePanelDirty();
                _draftName = value;
                OnPropertyChanged();
            }
        }

        private string _draftIndex = "";
        public string DraftIndex
        {
            get => _draftIndex;
            set
            {
                if (!Regex.IsMatch(value, @"^\d*$"))
                    return;

                MarkStatePanelDirty();
                _draftIndex = value;
                OnPropertyChanged();
            }
        }

        private string _draftOutput = "";
        public string DraftOutput
        {
            get => _draftOutput;
            set
            {
                if (!Regex.IsMatch(value, @"^\d*$"))
                    return;

                MarkStatePanelDirty();
                _draftOutput = value;
                OnPropertyChanged();
            }
        }

        private StateViewModel? _draftParent;
        public StateViewModel? DraftParent
        {
            get => _draftParent;
            set
            {
                var newValue = ReferenceEquals(value, _noParentOption) ? null : value;
                if (_draftParent == newValue) return;

                _draftParent = newValue;
                MarkStatePanelDirty();

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
            set
            {
                _childToAdd = value;
                OnPropertyChanged();
                RaiseAllCanExecute();
            }
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

        private List<string> listOfTransitionsInputsOfSelectedState = new List<string>();

        private string _newTransitionInput = "";
        public string NewTransitionInput
        {
            get => _newTransitionInput;
            set
            {
                if (!Regex.IsMatch(value, @"^\d*$"))
                    return;

                if (listOfTransitionsInputsOfSelectedState.Contains(value))
                    return;

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
        // --------- INPUTS DEFINITIONS ----------
        public ObservableCollection<InputModelData> InputsDef => _processController.InputsDef;

        private string oldID_Input_temp = "";
        private InputModelData? _selectedInput;
        public InputModelData? SelectedInput
        {
            get => _selectedInput;
            set
            {
                _selectedInput = value;
                oldID_Input_temp = _selectedInput?.ID;

                OnPropertyChanged();
                RaiseAllCanExecute();

            }
        }
        // --------- OUTPUT DEFINITIONS ----------
        public ObservableCollection<OutputModelData> OutputsDef => _processController.OutputsDef;

        private string oldID_Output_temp = "";
        private OutputModelData? _selectedOutput;
        public OutputModelData? SelectedOutput
        {
            get => _selectedOutput;
            set
            {
                _selectedOutput = value;
                oldID_Output_temp = _selectedOutput?.ID;

                OnPropertyChanged();
                RaiseAllCanExecute();
            }
        }

        // --------- PUBLIC API CALLED FROM WINDOW ----------
        public void SelectVertex(GraphVertex? v)
        {
            if (StatePanelDataChange)
            {
                Debug.WriteLine($"StatePanelDataChange");
                var decision = PopUpWindows.ConfirmStateSelectionIfDiff();

                if (decision == PopUpWindows.ConfirmChangeResult.Cancel)
                {
                    OnPropertyChanged(nameof(IsSelectedVertex));
                    OnPropertyChanged(nameof(IsSelectedVertexAndSection));
                    OnPropertyChanged(nameof(SelectedState));
                    OnPropertyChanged(nameof(StatePanelDataChange));
                    return;
                }
                if (decision == PopUpWindows.ConfirmChangeResult.Apply)
                {
                    StatePanelDataChange = false;
                    Apply();
                }
                if (decision == PopUpWindows.ConfirmChangeResult.Discard)
                {
                    StatePanelDataChange = false;
                }

            }

            SelectedVertex = v;

            if (v?.State == null)
            {
                SelectedState = null;
                OnPropertyChanged(nameof(IsSelectedVertex));
                OnPropertyChanged(nameof(IsSelectedVertexAndSection));
                OnPropertyChanged(nameof(StatePanelDataChange));
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

            OnPropertyChanged(nameof(IsSelectedVertex));
            OnPropertyChanged(nameof(IsSelectedVertexAndSection));
            OnPropertyChanged(nameof(StatePanelDataChange));

            FillListTransInputs();
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

                vm.Parent = m.Parent != null && _processController.modelToViewModel.TryGetValue(m.Parent, out var pvm) ? pvm : null;
            }

            // to keep selection
            if (SelectedState != null && _processController.modelToViewModel.TryGetValue(SelectedState.StateModel, out var newSel))
                SelectedState = newSel;

            RefreshCandidates();
            RefreshTransitionsFilter();
            //RaiseAllCanExecute();

            OnPropertyChanged(nameof(InputsDef));
            OnPropertyChanged(nameof(OutputsDef));
        }

        // --------- INTERNAL HELPERS ----------
        private void LoadDraftFromSelected()
        {
            _supressStatePanelDirty = true;
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
            DraftOutput = (SelectedState.Output == "-1") ? "" : (SelectedState.Output ?? "");
            DraftParent = SelectedState.Parent;

            foreach (var ch in SelectedState.SubStates)
                DraftChildren.Add(ch);

            UpdateDraftMarkers();

            StatePanelDataChange = false;

            _supressStatePanelDirty = false;
        }
        private void UpdateDraftMarkers()
        {
            if (SelectedState == null) return;

            var draftModels = new HashSet<StateModelData>(DraftChildren.Select(c => c.StateModel));

            var added = draftModels.Except(_originalChildren).ToList();
            var removed = _originalChildren.Except(draftModels).ToList();

            foreach (var m in added)
            {
                Debug.WriteLine($"ADDED - {m.Name} {m.FullIndex}");
                if (_processController.modelToViewModel.TryGetValue(m, out var vm))
                    vm.IsDraftAdded = true;
            }

            foreach (var m in removed)
            {
                Debug.WriteLine($"REMOVED - {m.Name} {m.FullIndex}");
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

            var parentList = FlatStates
                .Where(vm => vm != SelectedState)
                .Where(vm => !IsDescendant(SelectedState.StateModel, vm.StateModel))
                .Where(vm => !draftChildModels.Contains(vm.StateModel))
                .ToList();

            parentList.Insert(0, _noParentOption);

            ParentCandidates = new ObservableCollection<StateViewModel>(parentList);

            var childList = FlatStates
                .Where(vm => vm != SelectedState)
                //.Where(vm => !IsDescendant(SelectedState.StateModel, vm.StateModel))
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
            AddStateAsSubStateCommand.RaiseCanExecuteChanged();
            AddStateAsSubStateCMCommand.RaiseCanExecuteChanged();
            DeleteWholeSelectedStateCommand.RaiseCanExecuteChanged();
            DeleteWholeStateCommand.RaiseCanExecuteChanged();
            DeleteSingleSelectedStateCommand.RaiseCanExecuteChanged();
            DeleteSingleStateCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
            SaveAsCommand.RaiseCanExecuteChanged();
            AddInputCommand.RaiseCanExecuteChanged();
            RemoveInputCommand.RaiseCanExecuteChanged();
            AddOutputCommand.RaiseCanExecuteChanged();
            RemoveOutputCommand.RaiseCanExecuteChanged();
        }

        private void FillListTransInputs()
        {
            foreach(TransitionModelData trans in SelectedState.StateModel.TransitionsData)
            {
                listOfTransitionsInputsOfSelectedState.Add(trans.Input);
            }
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

            _statePanelDataChange = false;

            SelectedState.Name = DraftName;
            SelectedState.Output = DraftOutput;
            //SelectedState.Index = DraftIndex;

            _processController.ClearPendingChildChanges();

            var currentChildren = SelectedState.StateModel.SubStatesData.ToList();
            var draftModels = DraftChildren.Select(c => c.StateModel).ToList();

            var toRemove = currentChildren.Where(m => !draftModels.Contains(m)).ToList();
            var toAdd = draftModels.Where(m => !currentChildren.Contains(m)).ToList();

            foreach (var rm in toRemove) _processController.Add_RemovePendingChild(rm);
            foreach (var ad in toAdd) _processController.Add_AddPendingChild(ad);

            var parentModel = DraftParent?.StateModel;
            _processController.ApplyPendingChildChanges(SelectedVertex, parentModel, DraftIndex);

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

        private void Save()
        {
            if (_processController.Save())
            {
                Debug.WriteLine($"Successfuly saved file");
            }
            RaiseAllCanExecute();
        }

        private void SaveAs()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save as",
                Filter = "XML files (*.xml)|*.xml",
                FileName = "process.xml"
            };

            if (sfd.ShowDialog() == true)
            {
                _processController.SaveAs(sfd.FileName);
            }

            RaiseAllCanExecute();
        }

        private void AddInput()
        {
            InputsDef.Add(new InputModelData
            {
                ID = _processController.GetNextInputID(),
                Name = "NEW_INPUT"
            });

            RaiseAllCanExecute();
        }

        private void RemoveInput()
        {
            InputsDef.Remove(SelectedInput!);
            SelectedInput = null;

            RaiseAllCanExecute();
        }

        public void CheckInNeedSort_Inputs()
        {
            if (SelectedInput.ID == oldID_Input_temp)
            {
                return;
            }

            _processController.SortInputsDefByIdInPlace();
        }
        public void CheckInNeedSort_Outputs()
        {
            if (SelectedOutput.ID == oldID_Output_temp)
            {
                return;
            }

            _processController.SortOutputsDefByIdInPlace();
        }

        private void AddOutput()
        {
            OutputsDef.Add(new OutputModelData
            {
                ID = _processController.GetNextOutputID(),
                Name = "NEW_OUTPUT",
                UpdateDefinition = false,
                UpdateParameters = false,
                UpdateCalibration = false,
                UpdateMeasuredData = false,
                UpdateProcessedData = false
            });

            RaiseAllCanExecute();
        }

        private void RemoveOutput()
        {
            OutputsDef.Remove(SelectedOutput!);
            SelectedOutput = null;

            RaiseAllCanExecute();
        }
        internal bool CheckDuplicity_InputID()
        {
            return InputsDef.Any(i => !ReferenceEquals(i, _selectedInput) && i.ID == _selectedInput.ID);
        }
        internal bool CheckDuplicity_OutputID()
        {
            return OutputsDef.Any(o => !ReferenceEquals(o, _selectedOutput) && o.ID == _selectedOutput.ID);
        }

        private readonly StateViewModel _noParentOption = new StateViewModel(new StateModelData
        {
            Name = "(no parent)",
            Index = "",
            FullIndex = ""
        });

        // --------- INotifyPropertyChanged ----------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


    }
}
