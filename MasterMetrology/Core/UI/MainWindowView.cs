using MasterMetrology.Core.UI.Controllers;
using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using MasterMetrology.Utils;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

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

        private bool firstTime = true;
        private DateTime _suppressStatePromptUntilUtc = DateTime.MinValue;
        public MainWindowView(ProcessController processController, PanAndZoomController panAndZoomController)
        {
            _processController = processController;
            _panAndZoomController = panAndZoomController;

            // Transitions view (filtrovaný pohľad)
            TransitionsView = CollectionViewSource.GetDefaultView(_processController.AllTransitions);

            // Commands
            ApplyCommand = new RelayCommand(Apply, () => SelectedState != null && SelectedVertex != null);
            AddChildCommand = new RelayCommand(() => { AddChild(); _processController.MarkDirty(); StatePanelDataChange = true; OnPropertyChanged(nameof(StatePanelDataChange)); }, () => SelectedState != null && ChildToAdd != null);
            RemoveChildCommand = new RelayCommand(() => { RemoveChild(); _processController.MarkDirty(); StatePanelDataChange = true; OnPropertyChanged(nameof(StatePanelDataChange)); }, () => SelectedState != null && SelectedChild != null);

            AddTransitionCommand = new RelayCommand(() => { AddTransition(); _processController.MarkDirty(); }, () => SelectedState != null && SelectedTransitionTarget != null && !string.IsNullOrWhiteSpace(ExtractInputId(NewTransitionInput)) && InputDefinitionExists(ExtractInputId(NewTransitionInput)) && !TransitionInputAlreadyUsed(ExtractInputId(NewTransitionInput)));
            RemoveTransitionCommand = new RelayCommand(() => { RemoveTransition(); _processController.MarkDirty(); }, () => SelectedTransition != null);

            ExitAppCommand = new RelayCommand(ExitApp);
            ImportFileCommand = new RelayCommand(ImportFile);
            CreateNewFileCommand = new RelayCommand(CreateNewFile, () => InputsDef.Count > 0 || OutputsDef.Count > 0 || FlatStates.Count > 0);
            CenterViewCommand = new RelayCommand(CenterView);
            SetZoomCommand = new RelayCommand(SetZoom);

            AddStateToRootCommand = new RelayCommand(() => { _processController.CreateNewRootStateAtViewCenter(); _processController.MarkDirty(); });
            AddStateToRootAtPointCommand = new RelayCommand(p => { if (p is Point pt) _processController.CreateNewRootStateAt(pt); _processController.MarkDirty(); }, p => p is Point);
            AddStateAsSubStateCommand = new RelayCommand(() => { _processController.CreateNewSubState(SelectedVertex!.State); _processController.MarkDirty(); }, () => SelectedState != null);
            AddStateAsSubStateCMCommand = new RelayCommand(p => { if (p is GraphVertex gv && gv.State != null) _processController.CreateNewSubState(gv.State); _processController.MarkDirty(); }, p => p is GraphVertex gv && gv.State != null);

            DeleteWholeSelectedStateCommand = new RelayCommand(() => ConfirmAndDeleteWholeState(SelectedVertex?.State), () => SelectedState != null);
            DeleteWholeStateCommand = new RelayCommand(p => { if (p is GraphVertex gv && gv.State != null) ConfirmAndDeleteWholeState(gv.State); }, p => p is GraphVertex gv && gv.State != null);

            DeleteSingleSelectedStateCommand = new RelayCommand(() => TryDeleteByKind(LastSelectionKind.State), () => SelectedVertex?.State != null);
            DeleteSingleStateCommand = new RelayCommand(p => { if (p is GraphVertex gv && gv.State != null) ConfirmAndDeleteSingleState(gv.State); }, p => p is GraphVertex gv && gv.State != null);

            SaveCommand = new RelayCommand(Save, () => _processController.CanSave);
            SaveAsCommand = new RelayCommand(SaveAs, () => _processController.CanSaveAs);
            UndoCommand = new RelayCommand(Undo, () => _processController.CanUndo);
            RedoCommand = new RelayCommand(Redo, () => _processController.CanRedo);

            AddInputCommand = new RelayCommand(() => { AddInput(); _processController.MarkDirty(); });
            RemoveInputCommand = new RelayCommand(() => TryDeleteByKind(LastSelectionKind.InputDefinition), () => SelectedInput != null);

            AddOutputCommand = new RelayCommand(() => { AddOutput(); _processController.MarkDirty(); });
            RemoveOutputCommand = new RelayCommand(() => TryDeleteByKind(LastSelectionKind.OutputDefinition), () => SelectedOutput != null);

            _processController.DataChanged += () =>
            {
                SaveCommand.RaiseCanExecuteChanged();
                SaveAsCommand.RaiseCanExecuteChanged();
                UndoCommand.RaiseCanExecuteChanged();
                RedoCommand.RaiseCanExecuteChanged();
            };

            _processController.UndoRedoStateChanged += () =>
            {
                UndoCommand.RaiseCanExecuteChanged();
                RedoCommand.RaiseCanExecuteChanged();
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
        public RelayCommand SetZoomCommand { get; }
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
        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }
        public RelayCommand AddInputCommand { get; }
        public RelayCommand RemoveInputCommand { get; }
        public RelayCommand AddOutputCommand { get; }
        public RelayCommand RemoveOutputCommand { get; }
        public RelayCommand CreateNewFileCommand { get; }

        // --------- SELECTION ----------
        public GraphVertex? SelectedVertex { get; set; }
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
                RefreshAvailableInputDefinitions();
                RefreshTransitionTargetCandidates();
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
                if (_draftOutput == value)
                    return;

                var outputId = ExtractOutputId(value);

                if (!Regex.IsMatch(outputId, @"^\d*$"))
                    return;

                MarkStatePanelDirty();
                _draftOutput = value;

                var matchedOutput = OutputsDef.FirstOrDefault(o => o.ID == outputId);

                if (!ReferenceEquals(_selectedOutputDefinition, matchedOutput))
                {
                    _selectedOutputDefinition = matchedOutput;
                    OnPropertyChanged(nameof(SelectedOutputDefinition));
                }

                OnPropertyChanged();
            }
        }

        private OutputModelData? _selectedOutputDefinition;
        public OutputModelData? SelectedOutputDefinition
        {
            get => _selectedOutputDefinition;
            set
            {
                _selectedOutputDefinition = value;

                if (value != null)
                    DraftOutput = value.DisplayText;

                MarkStatePanelDirty();
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
            set
            {
                _selectedChild = value;

                if (value != null)
                    SetLastSelection(LastSelectionKind.Child);
                else if (_lastSelectionKind == LastSelectionKind.Child)
                    _lastSelectionKind = LastSelectionKind.None;

                OnPropertyChanged();
                RaiseAllCanExecute();
            }
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

                if (value != null)
                    SetLastSelection(LastSelectionKind.Transition);
                else if (_lastSelectionKind == LastSelectionKind.Transition)
                    _lastSelectionKind = LastSelectionKind.None;

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
                var inputId = ExtractInputId(value);

                if (inputId == _newTransitionInput)
                    return;

                if (!Regex.IsMatch(inputId, @"^\d*$"))
                    return;

                if (_selectedInputDefinition != null && _selectedInputDefinition.ID != inputId)
                {
                    _selectedInputDefinition = null;
                    OnPropertyChanged(nameof(SelectedInputDefinition));
                }

                _newTransitionInput = inputId;
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

        private ObservableCollection<InputModelData> _availableInputDefinitions = new();
        public ObservableCollection<InputModelData> AvailableInputDefinitions
        {
            get => _availableInputDefinitions;
            private set
            {
                _availableInputDefinitions = value;
                OnPropertyChanged();
            }
        }
        private InputModelData? _selectedInputDefinition;
        public InputModelData? SelectedInputDefinition
        {
            get => _selectedInputDefinition;
            set
            {
                _selectedInputDefinition = value;

                if (value != null)
                    NewTransitionInput = value.ID;

                OnPropertyChanged();
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

                if (value != null)
                    SetLastSelection(LastSelectionKind.InputDefinition);
                else if (_lastSelectionKind == LastSelectionKind.InputDefinition)
                    _lastSelectionKind = LastSelectionKind.None;

                OnPropertyChanged();
                RaiseAllCanExecute();

            }
        }
        private ObservableCollection<StateViewModel> _transitionTargetCandidates = new();
        public ObservableCollection<StateViewModel> TransitionTargetCandidates
        {
            get => _transitionTargetCandidates;
            private set
            {
                _transitionTargetCandidates = value;
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

                if (value != null)
                    SetLastSelection(LastSelectionKind.OutputDefinition);
                else if (_lastSelectionKind == LastSelectionKind.OutputDefinition)
                    _lastSelectionKind = LastSelectionKind.None;

                OnPropertyChanged();
                RaiseAllCanExecute();
            }
        }

        // --------- DEBUG MODE ----------

        public bool IsDeveloperMode
        {
            get => Config.DEBUG_MODE;
            set
            {
                if (Config.DEBUG_MODE == value) return;
                Config.DEBUG_MODE = value;
                OnPropertyChanged();
                RaiseAllCanExecute();
            }
        }

        // --------- PUBLIC API CALLED FROM WINDOW ----------
        public void SelectVertex(GraphVertex? v)
        {
            var previousSelectedVertex = SelectedVertex;
            var previousSelectedFullIndex = previousSelectedVertex?.State?.FullIndex;

            var suppressPrompt = DateTime.UtcNow <= _suppressStatePromptUntilUtc;
            if (suppressPrompt)
            {
                StatePanelDataChange = false;
            }
            else if (StatePanelDataChange)
            {
                if (Config.DEBUG_MODE)
                    Debug.WriteLine($"StatePanelDataChange");

                var decision = PopUpWindows.DialogWindow(
                    "Unsaved changes",
                    "Save changed data of state?",
                    ["Save", "Discard", "Cancel"]
                    );

                if (decision == PopUpWindows.ConfirmChangeResult.Cancel)
                {
                    _processController.RestoreVisualSelection(previousSelectedFullIndex);

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

            SetLastSelection(LastSelectionKind.State);

            OnPropertyChanged(nameof(IsSelectedVertex));
            OnPropertyChanged(nameof(IsSelectedVertexAndSection));
            OnPropertyChanged(nameof(StatePanelDataChange));
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
            RefreshAvailableInputDefinitions();
            RefreshTransitionTargetCandidates();
            RaiseAllCanExecute();

            OnPropertyChanged(nameof(InputsDef));
            OnPropertyChanged(nameof(OutputsDef));
        }

        // --------- INTERNAL HELPERS ----------
        private readonly HashSet<StateViewModel> _draftFlagged = new HashSet<StateViewModel>();
        private void LoadDraftFromSelected()
        {
            _supressStatePanelDirty = true;
            DraftChildren.Clear();
            ChildToAdd = null;
            SelectedChild = null;

            ClearDraftFlags();

            if (SelectedState == null)
            {
                DraftName = "";
                DraftIndex = "";
                DraftOutput = "";
                SelectedOutputDefinition = null;
                NewTransitionInput = "";
                SelectedInputDefinition = null;
                DraftParent = null;
                _originalChildren = new HashSet<StateModelData>();
                _originalParentModel = null;
                _supressStatePanelDirty = false;
                return;
            }

            _originalChildren = new HashSet<StateModelData>(SelectedState.StateModel.SubStatesData);
            _originalParentModel = SelectedState.StateModel.Parent;

            DraftName = SelectedState.Name ?? "";
            DraftIndex = SelectedState.Index ?? "";
            var selectedOutputId = (SelectedState.Output == "-1") ? "" : (SelectedState.Output ?? "");
            SelectedOutputDefinition = OutputsDef.FirstOrDefault(o => o.ID == selectedOutputId);
            DraftOutput = SelectedOutputDefinition?.DisplayText ?? selectedOutputId;
            NewTransitionInput = "";
            SelectedInputDefinition = null;
            DraftParent = SelectedState.Parent;

            foreach (var ch in SelectedState.SubStates)
                DraftChildren.Add(ch);

            UpdateDraftMarkers();

            StatePanelDataChange = false;

            _supressStatePanelDirty = false;
        }

        private void ClearDraftFlags()
        {
            foreach (var vm in _draftFlagged)
            {
                vm.IsDraftAdded = false;
                vm.IsDraftRemoved = false;
            }
            _draftFlagged.Clear();
        }

        private void UpdateDraftMarkers()
        {
            if (SelectedState == null) return;

            var draftModels = new HashSet<StateModelData>(DraftChildren.Select(c => c.StateModel));

            var added = draftModels.Except(_originalChildren).ToList();
            var removed = _originalChildren.Except(draftModels).ToList();

            foreach (var m in added)
            {
                if (Config.DEBUG_MODE)
                    Debug.WriteLine($"ADDED - {m.Name} {m.FullIndex}");

                if (_processController.modelToViewModel.TryGetValue(m, out var vm))
                    vm.IsDraftAdded = true;
            }

            foreach (var m in removed)
            {
                if (Config.DEBUG_MODE)
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
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
            AddInputCommand.RaiseCanExecuteChanged();
            RemoveInputCommand.RaiseCanExecuteChanged();
            AddOutputCommand.RaiseCanExecuteChanged();
            RemoveOutputCommand.RaiseCanExecuteChanged();
            CreateNewFileCommand.RaiseCanExecuteChanged();
        }

        private void RefreshAvailableInputDefinitions()
        {
            if (SelectedState == null)
            {
                AvailableInputDefinitions = new ObservableCollection<InputModelData>(InputsDef);
                return;
            }

            var usedInputIds = SelectedState.StateModel.TransitionsData
                .Select(t => t.Input)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            var available = InputsDef
                .Where(i => !usedInputIds.Contains(i.ID))
                .OrderBy(i => int.TryParse(i.ID, out var n) ? n : int.MaxValue)
                .ThenBy(i => i.ID)
                .ToList();

            AvailableInputDefinitions = new ObservableCollection<InputModelData>(available);
        }
        private string ExtractInputId(string? value)
        {
            var text = value?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(text))
                return "";

            var dashIndex = text.IndexOf(" - ");
            if (dashIndex >= 0)
                return text.Substring(0, dashIndex).Trim();

            return text;
        }

        private bool TransitionInputAlreadyUsed(string inputId)
        {
            if (SelectedState?.StateModel?.TransitionsData == null)
                return false;

            return SelectedState.StateModel.TransitionsData
                .Any(t => ExtractInputId(t.Input) == inputId);
        }

        private void RefreshTransitionTargetCandidates()
        {
            if (SelectedState == null)
            {
                TransitionTargetCandidates = new ObservableCollection<StateViewModel>(
                    FlatStates.Where(s => !s.IsSection));
                return;
            }

            TransitionTargetCandidates = new ObservableCollection<StateViewModel>(
                FlatStates.Where(s => !s.IsSection));
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

            var inputId = ExtractInputId(NewTransitionInput);

            if (!InputDefinitionExists(inputId))
            {
                MessageBox.Show(
                    $"Input {inputId} does not have a definition.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            if (TransitionInputAlreadyUsed(inputId))
            {
                MessageBox.Show(
                    $"Transition with input {inputId} already exists for this state.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _processController.AddTransition(SelectedState, inputId, SelectedTransitionTarget);

            NewTransitionInput = "";
            SelectedInputDefinition = null;

            RefreshTransitionsFilter();
            RefreshAvailableInputDefinitions();
            RaiseAllCanExecute();
        }

        private void RemoveTransition()
        {
            if (SelectedTransition == null) return;
            _processController.DeleteTransition(SelectedTransition);
            SelectedTransition = null;
            RefreshTransitionsFilter();
            RefreshAvailableInputDefinitions();
        }

        private void Apply()
        {
            if (SelectedState == null || SelectedVertex == null) return;

            var newIndex = DraftIndex?.Trim();
            if (string.IsNullOrWhiteSpace(newIndex))
            {
                MessageBox.Show(
                    "ID must be entered.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var parentModel = DraftParent?.StateModel;
            if (!_processController.IsStateIndexAvailableForApply(SelectedState.StateModel, parentModel, newIndex))
            {
                MessageBox.Show(
                    $"ID {newIndex} already exists on this level.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var outputText = DraftOutput?.Trim();
            var outputId = ExtractOutputId(outputText);

            if (!SelectedState.IsSection)
            {
                if (string.IsNullOrWhiteSpace(outputId))
                {
                    MessageBox.Show(
                        "Output must be selected or entered.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!OutputDefinitionExists(outputId))
                {
                    MessageBox.Show(
                        $"Output {outputId} does not have a definition.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            _statePanelDataChange = false;

            SelectedState.Name = DraftName;
            SelectedState.Output = outputId;

            _processController.ClearPendingChildChanges();

            var currentChildren = SelectedState.StateModel.SubStatesData.ToList();
            var draftModels = DraftChildren.Select(c => c.StateModel).ToList();

            var toRemove = currentChildren.Where(m => !draftModels.Contains(m)).ToList();
            var toAdd = draftModels.Where(m => !currentChildren.Contains(m)).ToList();

            foreach (var rm in toRemove) _processController.Add_RemovePendingChild(rm);
            foreach (var ad in toAdd) _processController.Add_AddPendingChild(ad);

            _processController.ApplyPendingChildChanges(SelectedVertex, parentModel, newIndex);

            RefreshFromController();

            _processController.MarkDirty();
            StatePanelDataChange = false;
            OnPropertyChanged(nameof(StatePanelDataChange));
        }

        private void ExitApp()
        {
            Application.Current.Shutdown();
        }

        private void ImportFile()
        {
            if (_processController.ProcessDecisionIfNotSavedDataToNewFile())
                return;

            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Chose file",
                Filter = "XML files (*.xml)|*.xml"
            };

            bool? response = ofd.ShowDialog();

            if (response == true)
            {

                _processController.ResetForNewFile();
                RefreshFromController();


                string filepath = ofd.FileName;

                _processController.LoadDataXML(filepath);
                RefreshFromController();
            }
        }

        private void CreateNewFile()
        {
            if (!_processController.ProcessDecisionIfNotSavedDataToNewFile())
                _processController.ResetForNewFile();
        }

        private void CenterView()
        {
            _panAndZoomController.CenterView();
        }

        private void Undo()
        {
            _suppressStatePromptUntilUtc = DateTime.UtcNow.AddSeconds(1);
            StatePanelDataChange = false;
            OnPropertyChanged(nameof(StatePanelDataChange));

            if (!_processController.Undo())
                return;

            StatePanelDataChange = false;
            OnPropertyChanged(nameof(StatePanelDataChange));
            RaiseAllCanExecute();
        }

        private void Redo()
        {
            _suppressStatePromptUntilUtc = DateTime.UtcNow.AddSeconds(1);
            StatePanelDataChange = false;
            OnPropertyChanged(nameof(StatePanelDataChange));

            if (!_processController.Redo())
                return;

            StatePanelDataChange = false;
            OnPropertyChanged(nameof(StatePanelDataChange));
            RaiseAllCanExecute();
        }

        private void Save()
        {
            if (_processController.Save())
            {
                if (Config.DEBUG_MODE)
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

            RefreshAvailableInputDefinitions();
            RaiseAllCanExecute();
        }

        private void RemoveInput()
        {
            InputsDef.Remove(SelectedInput!);
            SelectedInput = null;

            RefreshAvailableInputDefinitions();
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

        private bool InputDefinitionExists(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            return InputsDef.Any(x => x.ID == id.Trim());
        }

        private bool OutputDefinitionExists(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            return OutputsDef.Any(x => x.ID == id.Trim());
        }

        private string ExtractOutputId(string? value)
        {
            var text = value?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(text))
                return "";

            var dashIndex = text.IndexOf(" - ");
            if (dashIndex >= 0)
                return text.Substring(0, dashIndex).Trim();

            return text;
        }

        private void SetZoom(object? parameter)
        {
            if (double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double zoom))
            {
                _panAndZoomController.SetZoom(zoom);
            }
        }

        // --------- Logic behind Delete key ----------

        private enum LastSelectionKind
        {
            None,
            State,
            Transition,
            InputDefinition,
            OutputDefinition,
            Child
        }

        private LastSelectionKind _lastSelectionKind = LastSelectionKind.None;

        private void SetLastSelection(LastSelectionKind kind)
        {
            _lastSelectionKind = kind;
        }

        public bool CanDeleteLastSelected()
        {
            return _lastSelectionKind switch
            {
                LastSelectionKind.State => DeleteSingleSelectedStateCommand.CanExecute(null),
                LastSelectionKind.Transition => RemoveTransitionCommand.CanExecute(null),
                LastSelectionKind.InputDefinition => RemoveInputCommand.CanExecute(null),
                LastSelectionKind.OutputDefinition => RemoveOutputCommand.CanExecute(null),
                LastSelectionKind.Child => RemoveChildCommand.CanExecute(null),
                _ => false
            };
        }

        public void DeleteLastSelected()
        {
            TryDeleteByKind(_lastSelectionKind);
        }

        private bool TryDeleteByKind(LastSelectionKind kind)
        {
            string message;
            Action executeDelete;

            switch (kind)
            {
                case LastSelectionKind.State:
                    if (SelectedVertex?.State == null) return false;
                    message = $"Do you want to delete selected state \"{SelectedState?.Name}\" ({SelectedState?.FullIndex})?";
                    executeDelete = () => DeleteSingleStateCore(SelectedVertex.State);
                    break;

                case LastSelectionKind.Transition:
                    if (SelectedTransition == null) return false;
                    message = $"Do you want to delete selected transition with input \"{SelectedTransition.TransitionData?.Input}\"?";
                    executeDelete = DeleteTransitionCore;
                    break;

                case LastSelectionKind.InputDefinition:
                    if (SelectedInput == null) return false;
                    message = $"Do you want to delete input definition \"{SelectedInput.DisplayText}\"?";
                    executeDelete = DeleteInputDefinitionCore;
                    break;

                case LastSelectionKind.OutputDefinition:
                    if (SelectedOutput == null) return false;
                    message = $"Do you want to delete output definition \"{SelectedOutput.DisplayText}\"?";
                    executeDelete = DeleteOutputDefinitionCore;
                    break;

                case LastSelectionKind.Child:
                    if (SelectedChild == null) return false;
                    message = $"Do you want to remove child \"{SelectedChild.Name}\" ({SelectedChild.FullIndex}) from selected state?";
                    executeDelete = DeleteChildCore;
                    break;

                default:
                    return false;
            }

            return ExecuteDeleteWithConfirmation(message, executeDelete);
        }

        private void ConfirmAndDeleteSingleState(StateModelData state)
        {
            if (state == null) return;

            var message = $"Do you want to delete selected state \"{state.Name}\" ({state.FullIndex})?";
            ExecuteDeleteWithConfirmation(message, () => DeleteSingleStateCore(state));
        }

        private void ConfirmAndDeleteWholeState(StateModelData? state)
        {
            if (state == null) return;

            var message = $"Do you want to delete whole section \"{state.Name}\" ({state.FullIndex})?";
            ExecuteDeleteWithConfirmation(message, () => DeleteWholeStateCore(state));
        }

        private bool ExecuteDeleteWithConfirmation(string message, Action executeDelete)
        {
            var result = MessageBox.Show(
                message,
                "Confirm delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return false;

            executeDelete();
            _lastSelectionKind = LastSelectionKind.None;
            return true;
        }

        private void DeleteSingleStateCore(StateModelData state)
        {
            if (_processController.DeleteSingleState(state))
                _processController.MarkDirty();
        }

        private void DeleteWholeStateCore(StateModelData state)
        {
            if (_processController.DeleteWholeState(state))
                _processController.MarkDirty();
        }

        private void DeleteTransitionCore()
        {
            RemoveTransition();
            _processController.MarkDirty();
        }

        private void DeleteInputDefinitionCore()
        {
            RemoveInput();
            _processController.MarkDirty();
        }

        private void DeleteOutputDefinitionCore()
        {
            RemoveOutput();
            _processController.MarkDirty();
        }

        private void DeleteChildCore()
        {
            RemoveChild();
            _processController.MarkDirty();
            StatePanelDataChange = true;
            OnPropertyChanged(nameof(StatePanelDataChange));
        }


        // --------- INotifyPropertyChanged ----------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


    }
}
