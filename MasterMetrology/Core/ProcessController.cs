using MasterMetrology.Models.Visual;
using System.Windows.Controls;
using System.Windows;
using MasterMetrology.Models.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using MasterMetrology.Core;
using MasterMetrology.Core.UI.Rendering;
using MasterMetrology.Core.UI.Controllers;
using System.ComponentModel;
using MasterMetrology.Utils;
using Microsoft.Win32;
using System.Collections;
using MasterMetrology.Core.UI;
using System.Text;
using MasterMetrology.Core.History;

namespace MasterMetrology
{
    internal class ProcessController
    {


        private Canvas viewPort;
        private Canvas diagramCanvas;
        private FrameworkElement diagramBorder;
        private PanAndZoomController panAndZoom;
        private ProcessHistoryService historyService = new ProcessHistoryService(maxUndoRedoSteps: 100);

        public event Action? DataChanged;
        public event Action? UndoRedoStateChanged;
        public event Action? GraphChanged;

        private FileReader _fileReader = new FileReader();
        private VisualRendering visualRender = new VisualRendering();

        public event Action<GraphVertex?>? VertexSelected;

        private ObservableCollection<InputModelData> inputsDefModelDatas = new ObservableCollection<InputModelData>();
        private ObservableCollection<OutputModelData> outputsDefModelDatas = new ObservableCollection<OutputModelData>();
        private List<StateModelData> statesModelDatas = new List<StateModelData>();

        public ObservableCollection<OutputModelData> OutputsDef => outputsDefModelDatas;
        public ObservableCollection<InputModelData> InputsDef => inputsDefModelDatas;
        public ObservableCollection<StateViewModel> StatesViewModel = new ObservableCollection<StateViewModel>();
        public ObservableCollection<TransitionViewModel> AllTransitions = new ObservableCollection<TransitionViewModel>();
        public Dictionary<StateModelData, StateViewModel> modelToViewModel = new Dictionary<StateModelData, StateViewModel>();

        private readonly List<StateModelData> _pendingAdds = new List<StateModelData>();
        private readonly List<StateModelData> _pendingRemoves = new List<StateModelData>();
        private string? _pendingParentFullIndex = null;

        private List<(string, string)> movedPairs = new List<(string oldFull, string newFull)>();

        private string? _selectedFullIndex;

        public ProcessController(Canvas viewPort, Canvas diagramCanvas, FrameworkElement diagramBorder, PanAndZoomController panAndZoom)
        {
            this.viewPort = viewPort;
            this.diagramCanvas = diagramCanvas;
            this.diagramBorder = diagramBorder;
            this.panAndZoom = panAndZoom;
            historyService.HistoryStateChanged += RaiseUndoRedoStateChanged;

            InitializeHistory(clearStacks: true, isDirty: false);
        }

        public bool CanUndo => historyService.CanUndo;
        public bool CanRedo => historyService.CanRedo;

        public bool CanSave => !string.IsNullOrWhiteSpace(filePath) && _isDirty;
        public bool CanSaveAs => (statesModelDatas.Count > 0 || inputsDefModelDatas.Count > 0 || outputsDefModelDatas.Count > 0) && _isDirty;
        private string filePath;
        public string? CurrentFilePath => filePath;

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                DataChanged?.Invoke();
            }
        }

        public void MarkDirty()
        {
            if (historyService.IsTrackingSuspended)
                return;

            historyService.RegisterMutation(CaptureSnapshot);
            IsDirty = true;
        }
        public void MarkSaved() => IsDirty = false;

        public bool Undo() => historyService.Undo(CaptureSnapshot, RestoreFromSnapshot);
        public bool Redo() => historyService.Redo(CaptureSnapshot, RestoreFromSnapshot);

        private void InitializeHistory(bool clearStacks, bool isDirty) => historyService.Initialize(CaptureSnapshot, clearStacks, isDirty);

        private void RaiseUndoRedoStateChanged()
        {
            UndoRedoStateChanged?.Invoke();
        }

        private void NotifyGraphChanged() => GraphChanged?.Invoke();

        private ProcessSnapshot CaptureSnapshot(bool isDirty)
            => historyService.CaptureSnapshot(inputsDefModelDatas, outputsDefModelDatas, statesModelDatas, _selectedFullIndex, isDirty);

        private void RestoreFromSnapshot(ProcessSnapshot snapshot)
        {
            using (historyService.SuspendTracking())
            {
                var currentStateSignature = historyService.CurrentSnapshot?.StateSignature;
                if (string.IsNullOrWhiteSpace(currentStateSignature))
                {
                    currentStateSignature = historyService.BuildStateSignatureFromStates(statesModelDatas);
                }

                var statesChanged = currentStateSignature != snapshot.StateSignature;

                inputsDefModelDatas.Clear();
                foreach (var input in snapshot.Inputs)
                {
                    inputsDefModelDatas.Add(new InputModelData
                    {
                        ID = input.Id,
                        Name = input.Name
                    });
                }

                outputsDefModelDatas.Clear();
                foreach (var output in snapshot.Outputs)
                {
                    outputsDefModelDatas.Add(new OutputModelData
                    {
                        ID = output.Id,
                        Name = output.Name,
                        UpdateDefinition = output.UpdateDefinition,
                        UpdateParameters = output.UpdateParameters,
                        UpdateCalibration = output.UpdateCalibration,
                        UpdateMeasuredData = output.UpdateMeasuredData,
                        UpdateProcessedData = output.UpdateProcessedData
                    });
                }

                if (statesChanged)
                {
                    statesModelDatas = historyService.RebuildStatesFromSnapshots(snapshot.Roots);
                    _selectedFullIndex = snapshot.SelectedFullIndex;

                    BuildViewModelTreeFromModels(statesModelDatas);
                    PopulateTransitions(statesModelDatas);
                    visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
                    NotifyGraphChanged();

                    if (!string.IsNullOrWhiteSpace(_selectedFullIndex) &&
                        FindStateByFullIndex(_selectedFullIndex) != null)
                    {
                        visualRender.RequestSelectVertex(_selectedFullIndex);
                    }
                    else
                    {
                        _selectedFullIndex = null;
                        visualRender.ClearSelectionState();
                        VertexSelected?.Invoke(null);
                    }
                }

                IsDirty = snapshot.IsDirty;
                DataChanged?.Invoke();
            }
        }

        public void LoadDataXML(string filePath)
        {
            if (filePath == null)
                ResetForNewFile();

            SaveOldXMLPath(filePath);

            try
            {
                var list = _fileReader.LoadDataFromFile(filePath);

                inputsDefModelDatas.Clear();
                foreach (var i in list.InputsDefinition)
                    inputsDefModelDatas.Add(i);

                outputsDefModelDatas.Clear();
                foreach (var o in list.OutputDefinition)
                    outputsDefModelDatas.Add(o);

                statesModelDatas = list.FullListStateModelData;
            }
            catch (Exception ex)
            {
                var sfd = MessageBox.Show(
                    "Not supported format.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                if (Config.DEBUG_MODE)
                    Debug.WriteLine($"[LOAD DATA] Couldn't not read file. -> {ex.Message}");

                return;
            }

            SortInputsDefByIdInPlace();
            SortOutputsDefByIdInPlace();

            BuildViewModelTreeFromModels(statesModelDatas);

            PopulateTransitions(statesModelDatas);

            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
            NotifyGraphChanged();
            _selectedFullIndex = null;
            MarkSaved();
            InitializeHistory(clearStacks: true, isDirty: false);
            DataChanged?.Invoke();
        }

        private void SaveOldXMLPath(string filePath)
        {
            this.filePath = filePath;
        }
        public bool Save(bool validateDefinitions = true)
        {
            if (validateDefinitions && !ConfirmProceedWithMissingDefinitionsCheck("save"))
                return false;

            if (!CanSave) return false;
            return SaveToFile(filePath);
        }

        public bool SaveAs(string newPath, bool validateDefinitions = true)
        {
            if (string.IsNullOrWhiteSpace(newPath)) return false;

            if (validateDefinitions && !ConfirmProceedWithMissingDefinitionsCheck("save"))
                return false;

            filePath = newPath;
            return SaveToFile(filePath);
        }

        private bool SaveToFile(string path)
        {
            if (statesModelDatas == null) return false;

            var dto = ProcessXmlExporter.Build(inputsDefModelDatas, outputsDefModelDatas, statesModelDatas);

            ProcessXmlWriter.Save(path, dto);
            MarkSaved();
            InitializeHistory(clearStacks: false, isDirty: false);

            return true;
        }


        private void PopulateTransitions(List<StateModelData> roots)
        {
            if (Config.DEBUG_MODE)
                Debug.WriteLine($"POLUTE TRANSITIONS");

            void doPopulate()
            {
                AllTransitions.Clear();

                void collect(StateModelData s)
                {
                    if (s.TransitionsData != null)
                    {
                        var fromVm = GetVmOfModel(s);

                        foreach (var t in s.TransitionsData)
                        {
                            var nextVm = GetVmOfModel(FindStateByFullIndex(t.NextStateId));

                            AllTransitions.Add(new TransitionViewModel(t, fromVm, nextVm));

                            if (Config.DEBUG_MODE)
                                Debug.WriteLine($"Transition - {s.Name} {s.FullIndex} -> {t.NextState.Name} {t.NextState.FullIndex}");
                        }
                    }

                    if (s.SubStatesData != null)
                    {
                        foreach (var sub in s.SubStatesData)
                            collect(sub);
                    }
                }

                foreach (var r in roots)
                {
                    collect(r);
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(doPopulate);
            else
                doPopulate();

        }

        private StateViewModel CreateVmRecursive(StateModelData model, StateViewModel? parentVm = null)
        {
            var vm = new StateViewModel(model) { Parent = parentVm };
            modelToViewModel[model] = vm;

            if (model.SubStatesData != null)
            {
                foreach (var ch in model.SubStatesData)
                {
                    var childVm = CreateVmRecursive(ch, vm);
                    vm.SubStates.Add(childVm);
                }
            }
            return vm;
        }

        private void BuildViewModelTreeFromModels(IEnumerable<StateModelData> roots)
        {
            // run on UI thread
            void build()
            {
                StatesViewModel.Clear();
                foreach (var r in roots)
                {
                    var rootVm = CreateVmRecursive(r, null);
                    StatesViewModel.Add(rootVm);
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(build);
            else
                build();
        }
        public List<StateModelData> GetFlatStates()
        {
            var result = new List<StateModelData>();
            if (statesModelDatas == null) return result;

            void collect(StateModelData s)
            {
                result.Add(s);
                if (s.SubStatesData != null)
                    foreach (var sub in s.SubStatesData) collect(sub);
            }

            foreach (var r in statesModelDatas) collect(r);
            return result;
        }
        private StateViewModel GetVmOfModel(StateModelData state)
        {
            return modelToViewModel.GetValueOrDefault(state);
        }
        public bool DeleteTransition(TransitionViewModel vm)
        {
            TransitionModelData transitionToDelete = vm.TransitionData;
            if (transitionToDelete == null) return false;

            var ownerVm = vm.FromState;
            if (ownerVm != null && ownerVm.StateModel.TransitionsData != null)
            {
                var removed = ownerVm.StateModel.TransitionsData.Remove(transitionToDelete);

                if (removed)
                {
                    if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(() => AllTransitions.Remove(vm));
                    }
                    else
                    {
                        AllTransitions.Remove(vm);
                    }

                    visualRender.RemoveTransition(transitionToDelete);
                    return true;
                }
            }

            return false;
        }
        public bool AddTransition(StateViewModel fromStateVm, string input, StateViewModel targetStateVm)
        {
            TransitionModelData newTransition = new TransitionModelData
            {
                Input = input,
                NextState = targetStateVm.StateModel,
                NextStateId = targetStateVm.FullIndex,
                FromState = fromStateVm.StateModel
            };

            if (fromStateVm.StateModel.TransitionsData == null)
            {
                fromStateVm.StateModel.TransitionsData = new ObservableCollection<TransitionModelData>();
            }

            fromStateVm.StateModel.TransitionsData.Add(newTransition);

            var newTransitionVm = new TransitionViewModel(newTransition, fromStateVm, targetStateVm);

            AllTransitions.Add(newTransitionVm);
            visualRender.AddTransition(newTransition);
            return true;
        }

        private StateModelData? FindStateByFullIndex(List<StateModelData> list, string fullIndex)
        {
            if (list == null || string.IsNullOrEmpty(fullIndex)) return null;

            foreach (var s in list)
            {
                if (s.FullIndex == fullIndex) return s;
                if (s.SubStatesData != null)
                {
                    var r = FindStateByFullIndex(s.SubStatesData.ToList(), fullIndex);
                    if (r != null) return r;
                }
            }
            return null;
        }
        public StateModelData? FindStateByFullIndex(string fullIndex)
        {
            return FindStateByFullIndex(statesModelDatas, fullIndex);
        }

        public void Add_AddPendingChild(StateModelData child)
        {
            if (!_pendingAdds.Contains(child)) _pendingAdds.Add(child);
        }

        public void Add_RemovePendingChild(StateModelData child)
        {
            if (!_pendingRemoves.Contains(child)) _pendingRemoves.Add(child);
        }

        public void ClearPendingChildChanges()
        {
            _pendingAdds.Clear();
            _pendingRemoves.Clear();
            _pendingParentFullIndex = null;
        }

        public IReadOnlyList<StateModelData> GetPendingAdds() => _pendingAdds.AsReadOnly();
        public IReadOnlyList<StateModelData> GetPendingRemoves() => _pendingRemoves.AsReadOnly();

        private void SetCurrentStateIndex(GraphVertex selectedVertex, List<StateModelData> statesOnSameLevel)
        {
            bool isIndexAvailable = true;
            var biggestIndex = 0;

            foreach (var state in statesOnSameLevel)
            {
                if (state.Index == selectedVertex.State.Index)
                {
                    isIndexAvailable = false;
                }
                if (int.Parse(state.Index) > biggestIndex)
                {
                    biggestIndex = int.Parse(state.Index);
                }
            }

            if (!isIndexAvailable)
            {
                selectedVertex.State.Index = $"{biggestIndex + 1}";
            }
        }

        /// <summary>
        /// Aplikuje pending adds/removes.
        /// Pri adds: presunie zo starého parenta pod _pendingParentFullIndex
        /// Pri removes: presunie dané položky na top-level (unparent)
        /// Pri každom presune nastaví novú Index + FullIndex pre ten uzol (nasledujúci AFTER existing max).
        /// </summary>
        public void ApplyPendingChildChanges(GraphVertex selectedVertex, StateModelData parent, string newIndex)
        {
            movedPairs = new List<(string oldFull, string newFull)>();

            if (Config.DEBUG_MODE)
                Debug.WriteLine($"pendingAdds-{_pendingAdds.Count} pendingRemoves-{_pendingRemoves.Count}");

            if (selectedVertex.State.Parent != parent)
            {
                if (parent == null)
                {
                    SetCurrentStateIndex(selectedVertex, statesModelDatas);

                    selectedVertex.State.Parent.SubStatesData.Remove(selectedVertex.State);
                    selectedVertex.State.Parent = parent;
                    statesModelDatas.Add(selectedVertex.State);
                }
                else
                {
                    SetCurrentStateIndex(selectedVertex, parent.SubStatesData.ToList());

                    if (selectedVertex.State.Parent == null)
                    {
                        statesModelDatas.Remove(selectedVertex.State);
                    }
                    else
                    {
                        selectedVertex.State.Parent.SubStatesData.Remove(selectedVertex.State);
                    }
                    parent.SubStatesData.Add(selectedVertex.State);
                    selectedVertex.State.Parent = parent;
                }
                UpdateIndexesRecursive(selectedVertex.State);
            }

            if (selectedVertex.State.Index != newIndex)
            {
                if (IsIndexAvailable(newIndex, selectedVertex.State.Parent))
                {
                    selectedVertex.State.Index = newIndex;
                }
                else
                {
                    selectedVertex.State.Index = GetNextIndexForParent(parent).ToString();
                }
                UpdateIndexesRecursive(selectedVertex.State);
            }

            foreach (var state in _pendingRemoves.ToList())
            {
                var oldFull = state.FullIndex ?? "";

                if (state.Parent != null)
                {
                    state.Parent.SubStatesData.Remove(state);
                    state.Parent = null;
                }
                else
                {
                    statesModelDatas.Remove(state);
                }


                if (!IsIndexAvailable(state.Index, null))
                {
                    var nextIdx = GetNextIndexForParent(null);
                    state.Index = nextIdx.ToString();
                }
                statesModelDatas.Add(state);
                UpdateIndexesRecursive(state);
            }

            // 2) ADDS -> move each pending add under selectedVertex.State, compute new index/full
            foreach (var state in _pendingAdds.ToList())
            {
                var oldFull = state.FullIndex;

                if (state.Parent != null)
                {
                    state.Parent.SubStatesData.Remove(state);
                }
                else
                {
                    statesModelDatas.Remove(state);
                }



                if (!IsIndexAvailable(state.Index, selectedVertex.State))
                {
                    var nextIdx = GetNextIndexForParent(selectedVertex.State);
                    state.Index = nextIdx.ToString();
                }

                selectedVertex.State.SubStatesData.Add(state);
                state.Parent = selectedVertex.State;

                var newFull = $"{selectedVertex.State.FullIndex}.{state.Index}";
                UpdateIndexesRecursive(state);
            }

            // 3) Update all transitions which referenced oldFull -> newFull
            foreach (var (oldFull, newFull) in movedPairs)
            {
                if (!string.IsNullOrEmpty(oldFull) && !string.IsNullOrEmpty(newFull) && oldFull != newFull)
                    UpdateTransitionReferencesForMovedNode(oldFull, newFull);
            }

            ClearPendingChildChanges();

            try
            {
                DeleteTransitionsForAllSectionStates();

                PopulateTransitions(statesModelDatas);

                visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
                NotifyGraphChanged();
            }
            catch (Exception ex)
            {
                if (Config.DEBUG_MODE)
                    Debug.WriteLine("OnPendingChildrenApplied handler failed: " + ex.Message);
            }

            MarkSaved();
        }

        public bool IsStateIndexAvailableForApply(StateModelData state, StateModelData? targetParent, string candidateIndex)
        {
            if (state == null || string.IsNullOrWhiteSpace(candidateIndex))
                return false;

            var normalized = candidateIndex.Trim();
            IEnumerable<StateModelData> siblings = targetParent == null
                ? statesModelDatas
                : targetParent.SubStatesData;

            foreach (var sibling in siblings)
            {
                if (ReferenceEquals(sibling, state))
                    continue;

                if (sibling.Index == normalized)
                    return false;
            }

            return true;
        }

        private bool IsIndexAvailable(string newIndex, StateModelData? parent)
        {
            if (parent == null)
            {
                if (statesModelDatas.Any(s => s.Index == newIndex)) return false;
            }
            else if (parent.SubStatesData.Any(ss => ss.Index == newIndex))
            {
                return false;
            }
            return true;
        }
        private void UpdateIndexesRecursive(StateModelData state)
        {
            var oldFullIndex = state.FullIndex;
            if (state.Parent != null)
            {
                state.FullIndex = $"{state.Parent.FullIndex}.{state.Index}";
            }
            else
            {
                state.FullIndex = state.Index;
            }

            movedPairs.Add((oldFullIndex, state.FullIndex));

            if (state.SubStatesData.Count() > 0)
            {
                foreach (var subState in state.SubStatesData)
                {
                    UpdateIndexesRecursive(subState);
                }
            }
        }

        /// <summary>
        /// Return next index (int) by his parent (null => top-level).
        /// </summary>
        private int GetNextIndexForParent(StateModelData? parentState)
        {
            IEnumerable<StateModelData> siblings;
            if (parentState == null)
            {
                siblings = statesModelDatas;
            }
            else
            {
                siblings = parentState.SubStatesData;
            }

            int max = 0;
            foreach (var s in siblings)
            {
                if (int.TryParse(s.Index, out var v))
                {
                    if (v > max) max = v;
                    continue;
                }
            }
            return max + 1;
        }
        /// <summary>
        /// Updates all transitions (FromStage / NextStage) - exact match.
        /// </summary>
        private void UpdateTransitionReferencesForMovedNode(string oldFull, string newFull)
        {
            if (string.IsNullOrEmpty(oldFull) || string.IsNullOrEmpty(newFull)) return;

            void walk(StateModelData s)
            {
                if (s.TransitionsData != null)
                {
                    foreach (var t in s.TransitionsData)
                    {
                        if (string.IsNullOrWhiteSpace(t.NextStateId)) continue;

                        if (t.NextStateId == oldFull)
                        {
                            t.NextStateId = newFull;
                        }
                    }
                }

                if (s.SubStatesData != null && s.SubStatesData.Count > 0)
                {
                    foreach (var sub in s.SubStatesData) walk(sub);
                }
            }

            foreach (var root in statesModelDatas)
            {
                walk(root);
            }
        }

        public StateModelData CreateNewRootStateAtViewCenter()
        {
            var p = panAndZoom.GetViewCenterWorld();
            return CreateNewStateInternal(parent: null, p);
        }

        public StateModelData CreateNewSubState(StateModelData parent)
        {
            return CreateNewStateInternal(parent, default);
        }

        private StateModelData CreateNewStateInternal(StateModelData? parent, Point world)
        {
            if (Config.DEBUG_MODE)
                Debug.WriteLine($"Creating new State. World pos: X:{world.X}, Y:{world.Y}");

            var nextIdx = GetNextIndexForParent(parent);
            var idxStr = nextIdx.ToString();

            var newState = new StateModelData
            {
                Name = "New state",
                Output = "0",
                Index = idxStr,
                Parent = parent,
                SubStatesData = new ObservableCollection<StateModelData>(),
                TransitionsData = new ObservableCollection<TransitionModelData>(),
            };

            newState.FullIndex = (parent == null) ? newState.Index : $"{parent.FullIndex}.{newState.Index}";

            if (parent == null)
            {
                statesModelDatas.Add(newState);
            }
            else
            {
                parent.SubStatesData.Add(newState);
                DeleteTransitionsForAllSectionStates();
            }

            // move vertex after render
            if (parent == null)
                visualRender.RequestPlaceVertex(newState.FullIndex, world);

            // re-render + transitions
            PopulateTransitions(statesModelDatas);
            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
            NotifyGraphChanged();

            visualRender.RequestSelectVertex(newState.FullIndex);

            DataChanged?.Invoke();

            if (Config.DEBUG_MODE)
                Debug.WriteLine($"Successfuly created.");

            return newState;
        }

        public bool DeleteWholeState(StateModelData state)
        {
            if (state == null) return false;

            bool clearSelection = ContainsFullIndex(state, _selectedFullIndex);
            var toDelete = new HashSet<StateModelData>();
            void collect(StateModelData s)
            {
                if (s == null || toDelete.Contains(s)) return;
                toDelete.Add(s);
                if (s.SubStatesData != null)
                    foreach (var ch in s.SubStatesData) collect(ch);
            }
            collect(state);

            var deletedIds = new HashSet<string>(toDelete.Select(s => s.FullIndex).Where(x => !string.IsNullOrWhiteSpace(x)));

            if (state.Parent != null)
                state.Parent.SubStatesData.Remove(state);
            else
                statesModelDatas.Remove(state);

            void pruneTransitions(StateModelData s)
            {
                if (s.TransitionsData != null)
                {
                    var rm = s.TransitionsData.Where(t => deletedIds.Contains(t.NextStateId)).ToList();
                    foreach (var t in rm) s.TransitionsData.Remove(t);
                }
                if (s.SubStatesData != null)
                    foreach (var ch in s.SubStatesData) pruneTransitions(ch);
            }
            foreach (var r in statesModelDatas) pruneTransitions(r);

            if (clearSelection)
                visualRender.ClearPendingSelection();

            PopulateTransitions(statesModelDatas);
            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
            NotifyGraphChanged();

            DataChanged?.Invoke();
            if (clearSelection)
            {
                visualRender.ClearSelectionState();
                VertexSelected?.Invoke(null);
                _selectedFullIndex = null;
            }

            return true;
        }

        public bool DeleteSingleState(StateModelData state)
        {
            if (state == null) return false;

            bool clearSelection = state.FullIndex == _selectedFullIndex;

            var parent = state.Parent;
            var children = state.SubStatesData?.ToList() ?? new List<StateModelData>();

            movedPairs = new List<(string oldFull, string newFull)>();

            if (parent != null)
                parent.SubStatesData.Remove(state);
            else
                statesModelDatas.Remove(state);

            foreach (var ch in children)
            {
                state.SubStatesData.Remove(ch);

                ch.Parent = parent;

                if (parent != null)
                    parent.SubStatesData.Add(ch);
                else
                    statesModelDatas.Add(ch);

                var nextIdx = GetNextIndexForParent(parent);
                ch.Index = nextIdx.ToString();

                UpdateIndexesRecursive(ch);
            }

            var deletedId = state.FullIndex;

            void prune(StateModelData s)
            {
                if (s.TransitionsData != null)
                {
                    var rm = s.TransitionsData
                        .Where(t => t.NextStateId == deletedId)
                        .ToList();
                    foreach (var t in rm) s.TransitionsData.Remove(t);
                }

                if (s.SubStatesData != null)
                    foreach (var sub in s.SubStatesData) prune(sub);
            }

            foreach (var r in statesModelDatas)
                prune(r);

            foreach (var (oldFull, newFull) in movedPairs)
            {
                if (!string.IsNullOrWhiteSpace(oldFull) &&
                    !string.IsNullOrWhiteSpace(newFull) &&
                    oldFull != newFull)
                {
                    UpdateTransitionReferencesForMovedNode(oldFull, newFull);
                }
            }

            if (clearSelection)
                visualRender.ClearPendingSelection();

            PopulateTransitions(statesModelDatas);
            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
            NotifyGraphChanged();

            DataChanged?.Invoke();
            if (clearSelection)
            {
                visualRender.ClearSelectionState();
                VertexSelected?.Invoke(null);
                _selectedFullIndex = null;
            }

            return true;
        }

        public void SetSelectedVertex(GraphVertex? vertex)
        {
            _selectedFullIndex = vertex?.State?.FullIndex;
        }

        private static bool ContainsFullIndex(StateModelData root, string? fullIndex)
        {
            if (string.IsNullOrWhiteSpace(fullIndex) || root == null) return false;
            if (root.FullIndex == fullIndex) return true;
            if (root.SubStatesData == null) return false;
            foreach (var ch in root.SubStatesData)
                if (ContainsFullIndex(ch, fullIndex)) return true;
            return false;
        }

        public StateModelData CreateNewRootStateAt(Point world)
        {
            return CreateNewStateInternal(parent: null, world);
        }

        public string GetNextInputID()
        {
            var max = inputsDefModelDatas
                .Select(i => i.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => int.TryParse(id, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            return (max + 1).ToString();
        }

        public string GetNextOutputID()
        {
            var max = outputsDefModelDatas
                .Select(o => o.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => int.TryParse(id, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            return (max + 1).ToString();
        }


        internal void SortInputsDefByIdInPlace()
        {
            var ordered = InputsDef
             .OrderBy(x => int.TryParse(x.ID, out var n) ? n : int.MaxValue)
             .ThenBy(x => x.ID)
             .ToList();

            for (int targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
            {
                var item = ordered[targetIndex];
                int currentIndex = InputsDef.IndexOf(item);
                if (currentIndex != targetIndex && currentIndex >= 0)
                    InputsDef.Move(currentIndex, targetIndex);
            }
        }
        internal void SortOutputsDefByIdInPlace()
        {
            var ordered = OutputsDef
             .OrderBy(x => int.TryParse(x.ID, out var n) ? n : int.MaxValue)
             .ThenBy(x => x.ID)
             .ToList();

            for (int targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
            {
                var item = ordered[targetIndex];
                int currentIndex = OutputsDef.IndexOf(item);
                if (currentIndex != targetIndex && currentIndex >= 0)
                    OutputsDef.Move(currentIndex, targetIndex);
            }
        }

        private bool _monitorWired;

        public void WireMonitorChanges()
        {
            if (_monitorWired)
                return;
            _monitorWired = true;

            InputsDef.CollectionChanged += InputsDef_CollectionChanged;
            OutputsDef.CollectionChanged += OutputsDef_CollectionChanged;

            foreach (var item in InputsDef) item.PropertyChanged += ItemChanged;
            foreach (var item in OutputsDef) item.PropertyChanged += ItemChanged;

            if (historyService.CurrentSnapshot == null)
                InitializeHistory(clearStacks: true, isDirty: IsDirty);
        }

        private void UnwireMonitorChangesIfWired()
        {
            if (!_monitorWired)
                return;
            _monitorWired = false;

            InputsDef.CollectionChanged -= InputsDef_CollectionChanged;
            OutputsDef.CollectionChanged -= OutputsDef_CollectionChanged;

            foreach (var item in InputsDef) item.PropertyChanged -= ItemChanged;
            foreach (var item in OutputsDef) item.PropertyChanged -= ItemChanged;
        }

        private void InputsDef_CollectionChanged(object? _, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (InputModelData it in e.NewItems)
                    it.PropertyChanged += ItemChanged;

            if (e.OldItems != null)
                foreach (InputModelData it in e.OldItems)
                    it.PropertyChanged -= ItemChanged;

            MarkDirty();
        }

        private void OutputsDef_CollectionChanged(object? _, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (OutputModelData it in e.NewItems)
                    it.PropertyChanged += ItemChanged;

            if (e.OldItems != null)
                foreach (OutputModelData it in e.OldItems)
                    it.PropertyChanged -= ItemChanged;

            MarkDirty();
        }

        private void ItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Config.DEBUG_MODE)
                Debug.WriteLine("Change");

            MarkDirty();
        }

        public bool ProcessDecisionExitWin()
        {
            //if (!ConfirmProceedWithMissingDefinitionsCheck("exit"))
            //    return true;

            return OpenCustomDialog(
                "Unsaved Changes",
                "You have unsaved changes. Would you Like to save them before exit?",
                ["Yes", "Don't save", "Cancel"],
                missingDefinitionName: "exit");
        }

        public bool ProcessDecisionIfNotSavedDataToNewFile()
        {
            //if (!ConfirmProceedWithMissingDefinitionsCheck("continue to new file"))
            //    return true;

            return OpenCustomDialog(
                "Unsaved data",
                "Your file is not saved. Would you like to save data before continuing to new file?",
                ["Yes", "Don't save", "Cancel"],
                missingDefinitionName: "continue to new file");
        }

        private bool OpenCustomDialog(string title, string message, ArrayList buttons, string missingDefinitionName)
        {
            var hasAnyProcessData = statesModelDatas.Count > 0 || inputsDefModelDatas.Count > 0 || outputsDefModelDatas.Count > 0;

            if (IsDirty && hasAnyProcessData)
            {
                var decision = PopUpWindows.DialogWindow(title, message, buttons);

                if (decision == PopUpWindows.ConfirmChangeResult.Cancel)
                {
                    return true;
                }
                else if (decision == PopUpWindows.ConfirmChangeResult.Apply)
                {
                    if (!ConfirmProceedWithMissingDefinitionsCheck(missingDefinitionName))
                        return true;
                    if (CanSave)
                    {
                        
                        if (!Save(validateDefinitions: true))
                            return true;
                    }
                    else
                    {
                        var sfd = new SaveFileDialog
                        {
                            Title = "Save as",
                            Filter = "XML files (*.xml)|*.xml",
                            FileName = "process.xml"
                        };

                        if (sfd.ShowDialog() == true)
                        {
                            if (!SaveAs(sfd.FileName, validateDefinitions: false))
                                return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
            return false;
        }

        private bool ConfirmProceedWithMissingDefinitionsCheck(string actionName)
        {
            var knownInputIds = new HashSet<string>(
                inputsDefModelDatas
                    .Select(i => NormalizeDefinitionId(i.ID))
                    .Where(id => !string.IsNullOrWhiteSpace(id)));

            var knownOutputIds = new HashSet<string>(
                outputsDefModelDatas
                    .Select(o => NormalizeDefinitionId(o.ID))
                    .Where(id => !string.IsNullOrWhiteSpace(id)));

            var missingInputIds = new HashSet<string>();
            var missingOutputIds = new HashSet<string>();

            foreach (var state in GetFlatStates())
            {
                var outputId = NormalizeDefinitionId(state.Output);
                if (!string.IsNullOrWhiteSpace(outputId) && !knownOutputIds.Contains(outputId) && !IsNoOutputSentinel(outputId))
                    missingOutputIds.Add(outputId);

                if (state.TransitionsData == null)
                    continue;

                foreach (var transition in state.TransitionsData)
                {
                    var inputId = NormalizeDefinitionId(transition?.Input);
                    if (!string.IsNullOrWhiteSpace(inputId) && !knownInputIds.Contains(inputId))
                        missingInputIds.Add(inputId);
                }
            }

            if (missingInputIds.Count == 0 && missingOutputIds.Count == 0)
                return true;

            var message = BuildMissingDefinitionsMessage(missingInputIds, missingOutputIds, actionName);
            var decision = PopUpWindows.DialogWindow(
                "Missing definitions",
                message,
                ["Continue", "Stop", "Cancel"]);

            return decision == PopUpWindows.ConfirmChangeResult.Apply;
        }

        private bool IsNoOutputSentinel(string outputId)
        {
            return outputId == "-1";
        }

        private static string BuildMissingDefinitionsMessage(HashSet<string> missingInputIds, HashSet<string> missingOutputIds, string actionName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Found references without definition:");

            if (missingInputIds.Count > 0)
                sb.AppendLine($"Missing input definitions: {string.Join(", ", OrderDefinitionIds(missingInputIds))}");

            if (missingOutputIds.Count > 0)
                sb.AppendLine($"Missing output definitions: {string.Join(", ", OrderDefinitionIds(missingOutputIds))}");

            sb.AppendLine();
            sb.Append($"Do you want to continue with {actionName}?");
            return sb.ToString();
        }

        private static IEnumerable<string> OrderDefinitionIds(IEnumerable<string> ids)
        {
            return ids
                .OrderBy(id => int.TryParse(id, out _) ? 0 : 1)
                .ThenBy(id => int.TryParse(id, out var n) ? n : int.MaxValue)
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeDefinitionId(string? rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                return "";

            var id = rawId.Trim();
            var dashIndex = id.IndexOf('-');
            if (dashIndex > 0)
                id = id.Substring(0, dashIndex).Trim();

            return id;
        }

        public void ResetForNewFile()
        {
            UnwireMonitorChangesIfWired();

            statesModelDatas.Clear();
            inputsDefModelDatas.Clear();
            outputsDefModelDatas.Clear();

            StatesViewModel.Clear();
            AllTransitions.Clear();
            modelToViewModel.Clear();

            _pendingAdds.Clear();
            _pendingRemoves.Clear();
            _pendingParentFullIndex = null;
            movedPairs.Clear();

            _selectedFullIndex = null;

            visualRender.ResetVisuals(viewPort);
            NotifyGraphChanged();

            VertexSelected?.Invoke(null);
            DataChanged?.Invoke();

            MarkSaved();
            filePath = null;
            InitializeHistory(clearStacks: true, isDirty: false);
            WireMonitorChanges();
        }

        private void DeleteTransitionsForAllSectionStates()
        {
            var allStates = GetFlatStates();

            var sectionStates = allStates
                    .Where(s => s.SubStatesData != null && s.SubStatesData.Count > 0)
                    .ToList();

            if (sectionStates.Count == 0)
                return;

            var sectionSet = new HashSet<StateModelData>(sectionStates);
            var sectionIds = new HashSet<string>(
                sectionStates
                    .Where(s => !string.IsNullOrWhiteSpace(s.FullIndex))
                    .Select(s => s.FullIndex)
            );

            foreach (var state in allStates)
            {
                if (state.TransitionsData == null || state.TransitionsData.Count == 0)
                    continue;

                var toRemove = state.TransitionsData
                    .Where(t =>
                        t != null &&
                        (
                            // outgoing zo section
                            sectionSet.Contains(state)
                            ||
                            // incoming do section podľa referencie
                            (t.NextState != null && sectionSet.Contains(t.NextState))
                            ||
                            // incoming do section podľa ID
                            (!string.IsNullOrWhiteSpace(t.NextStateId) && sectionIds.Contains(t.NextStateId))
                        ))
                    .ToList();

                foreach (var tr in toRemove)
                    state.TransitionsData.Remove(tr);
            }
        }

        public void RestoreVisualSelection(string? fullIndex)
        {
            visualRender.RestoreSelection(fullIndex);
        }

    }
}
