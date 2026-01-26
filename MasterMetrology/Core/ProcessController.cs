using MasterMetrology.Models.Visual;
using System.Windows.Controls;
using System.Windows;
using MasterMetrology.Models.Data;
using MasterMetrology.Core.Rendering;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace MasterMetrology
{
    internal class ProcessController(Canvas viewPort, Canvas diagramCanvas)
    {
        private Canvas viewPort = viewPort;
        private Canvas diagramCanvas = diagramCanvas;
        private FileReader _fileReader = new FileReader();
        private VisualRendering visualRender = new VisualRendering();

        public event Action<GraphVertex?> VertexSelected;

        private List<InputsDefModelData> inputsDefModelDatas;
        private List<OutputModelData> outputsDefModelDatas;
        private List<StateModelData> statesModelDatas = new List<StateModelData>();
        
        public ObservableCollection<StateViewModel> StatesViewModel = new ObservableCollection<StateViewModel>();
        public ObservableCollection<TransitionViewModel> AllTransitions = new ObservableCollection<TransitionViewModel>();
        public Dictionary<StateModelData, StateViewModel> modelToViewModel = new Dictionary<StateModelData, StateViewModel>();

        private readonly List<StateModelData> _pendingAdds = new List<StateModelData>();
        private readonly List<StateModelData> _pendingRemoves = new List<StateModelData>();
        private string? _pendingParentFullIndex = null;

        private List<(string, string)> movedPairs = new List<(string oldFull, string newFull)>();

        private string filePath;

        public void LoadDataXML(string filePath)
        {
            SaveOldXMLPath(filePath);
            var list = _fileReader.LoadDataFromFile(filePath);

            inputsDefModelDatas = list.InputsDefinition;
            outputsDefModelDatas = list.OutputDefinition;
            statesModelDatas = list.FullListStateModelData;

            BuildViewModelTreeFromModels(statesModelDatas);

            PopulateTransitions(statesModelDatas);
            
            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
        }

        private void SaveOldXMLPath(string filePath)
        {
            this.filePath = filePath;
        }

        private void PopulateTransitions(List<StateModelData> roots)
        {
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
                //modelToViewModel.Clear();
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
        public List<StateViewModel> GetFlatStateViewModels()
        {
            var res = new List<StateViewModel>();
            void collect(StateViewModel svm)
            {
                res.Add(svm);
                foreach (var ch in svm.SubStates) collect(ch);
            }
            foreach (var root in StatesViewModel) collect(root);
            return res;
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

        public void Remove_AddPendingChild(StateModelData child)
        {
            _pendingAdds.Remove(child);
        }

        public void Add_RemovePendingChild(StateModelData child)
        {
            if (!_pendingRemoves.Contains(child)) _pendingRemoves.Add(child);
        }

        public void Remove_RemovePendingChild(StateModelData child)
        {
            _pendingRemoves.Remove(child);
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

                statesModelDatas.Add(state);
                var nextIdx = GetNextIndexForParent(null);
                state.Index = nextIdx.ToString();
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

                selectedVertex.State.SubStatesData.Add(state);
                state.Parent = selectedVertex.State;

                var nextIdx = GetNextIndexForParent(selectedVertex.State);
                state.Index = nextIdx.ToString();
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
                PopulateTransitions(statesModelDatas);

                visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v), diagramCanvas);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OnPendingChildrenApplied handler failed: " + ex.Message);
            }
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
    }
}