using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Xml;
using MasterMetrology.Models.Data;
using MasterMetrology.Core.Rendering;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Diagnostics;

namespace MasterMetrology
{
    internal class ProcessController(Canvas viewPort)
    {
        private Canvas viewPort = viewPort;
        private FileReader _fileReader = new FileReader();
        private VisualRendering visualRender = new VisualRendering();

        public event Action<GraphVertex> VertexSelected;

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
            
            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v));
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
                            //t.FromState = s;
                            //t.NextState = FindStateByFullIndex(t.NextStateId);
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
        
            // ensure SubStates collection exists on VM (your StateViewModel should have SubStates ObservableCollection)
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


        // Vrátí "flattovaný" zoznam všetkých states (pre ComboBox target)
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

        // Delete prechod - odstráni z dát a z grafu
        public bool DeleteTransition(TransitionViewModel vm)
        {
            TransitionModelData transitionToDelete = vm.TransitionData;
            if (transitionToDelete == null) return false;

            //var owner = FindStateByFullIndex(statesModelDatas, vm.Transition.FromState.FullIndex);
            var ownerVm = vm.FromState;
            if (ownerVm != null && ownerVm.StateModel.TransitionsData != null)
            {
                var removed = ownerVm.StateModel.TransitionsData.Remove(transitionToDelete);
                // odstrániť z UI kolekcie (na UI vlákne)
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

        // Add nový transition (cez UI): pridá do dát aj do grafu a do AllTransitions
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
            //visualRender.AddTransition(newTransition);

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
        public StateModelData? FindParentByFullIndex(string childFullIndex)
        {
            if (statesModelDatas == null || string.IsNullOrEmpty(childFullIndex)) return null;
            return FindParentRecursive(statesModelDatas, childFullIndex);
        }

        private StateModelData? FindParentRecursive(List<StateModelData> list, string childFullIndex)
        {
            if (list == null) return null;
            foreach (var s in list)
            {
                if (s.SubStatesData != null)
                {
                    // priamy potomok?
                    if (s.SubStatesData.Any(sub => sub.FullIndex == childFullIndex))
                        return s;

                    // hľadaj hlbšie
                    var parent = FindParentRecursive(s.SubStatesData.ToList(), childFullIndex);
                    if (parent != null) return parent;
                }
            }
            return null;
        }

        // --- PUBLIC HOOK: UI môže subscribe a zavolať render / refresh ---
        public Action<List<StateModelData>>? OnPendingChildrenApplied { get; set; }

        // ---------------- UI metódy (volaj z code-behind) ----------------

        public void Add_AddPendingChild(StateModelData child)
        {
            //if (string.IsNullOrWhiteSpace(childFullIndex)) return;
            if (!_pendingAdds.Contains(child)) _pendingAdds.Add(child);
        }

        public void Remove_AddPendingChild(StateModelData child)
        {
            //if (string.IsNullOrWhiteSpace(child)) return;
            _pendingAdds.Remove(child);
        }

        public void Add_RemovePendingChild(StateModelData child)
        {
            //if (string.IsNullOrWhiteSpace(childFullIndex)) return;
            if (!_pendingRemoves.Contains(child)) _pendingRemoves.Add(child);
        }

        public void Remove_RemovePendingChild(StateModelData child)
        {
            //if (string.IsNullOrWhiteSpace(child)) return;
            _pendingRemoves.Remove(child);
        }

        /// <summary>Set pending parent (null or empty means top-level).</summary>
        public void SetPendingParent(string? parentFullIndex)
        {
            _pendingParentFullIndex = string.IsNullOrWhiteSpace(parentFullIndex) ? null : parentFullIndex;
        }

        public void ClearPendingChildChanges()
        {
            _pendingAdds.Clear();
            _pendingRemoves.Clear();
            _pendingParentFullIndex = null;
        }

        public IReadOnlyList<StateModelData> GetPendingAdds() => _pendingAdds.AsReadOnly();
        public IReadOnlyList<StateModelData> GetPendingRemoves() => _pendingRemoves.AsReadOnly();
        public string? GetPendingParent() => _pendingParentFullIndex;

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
        /// Pri adds: presunie zo starého parenta pod _pendingParentFullIndex (null => top-level)
        /// Pri removes: presunie dané položky na top-level (unparent)
        /// Pri každom presune nastaví novú Index + FullIndex pre ten uzol (nasledujúci AFTER existing max).
        /// Po aplikácii zavolá OnPendingChildrenApplied ak je prihlásené.
        /// </summary>
        public void ApplyPendingChildChanges(GraphVertex selectedVertex, StateModelData parent)
        {
            /*if ((_pendingAdds.Count == 0) && (_pendingRemoves.Count == 0))
            {
                ClearPendingChildChanges();
                return;
            }*/

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


            // 1) REMOVALS -> unparent to top-level, compute new index/full and collect moved pair
            foreach (var state in _pendingRemoves.ToList())
            {
                var oldFull = state.FullIndex ?? "";

                // remove from old parent or from top-level list
                if (state.Parent != null)
                {
                    state.Parent.SubStatesData.Remove(state);
                    state.Parent = null;
                }
                else
                {
                    // if it was top-level already, remove from statesModelDatas to re-add later
                    statesModelDatas.Remove(state);
                }

                // add as top-level and compute new index/full
                statesModelDatas.Add(state);
                var nextIdx = GetNextIndexForParent(null);
                state.Index = nextIdx.ToString();
                //UpdateFullIndexRecursive(state, state.Index); // sets FullIndex and children
                UpdateIndexesRecursive(state);
                //movedPairs.Add((oldFull, state.FullIndex));
            }

            // 2) ADDS -> move each pending add under selectedVertex.State, compute new index/full
            foreach (var state in _pendingAdds.ToList())
            {
                var oldFull = state.FullIndex ?? "";

                // remove from old location
                if (state.Parent != null)
                {
                    state.Parent.SubStatesData.Remove(state);
                }
                else
                {
                    statesModelDatas.Remove(state);
                }

                // add under selected vertex
                selectedVertex.State.SubStatesData.Add(state);
                state.Parent = selectedVertex.State;

                // assign next index within new parent and update fullindex recursively
                var nextIdx = GetNextIndexForParent(selectedVertex.State);
                state.Index = nextIdx.ToString();
                var newFull = $"{selectedVertex.State.FullIndex}.{state.Index}";
                //UpdateFullIndexRecursive(state, newFull);
                UpdateIndexesRecursive(state);
                //movedPairs.Add((oldFull, state.FullIndex));
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
                // re-populate transitions (this will resolve NextState from NextStateId)
                PopulateTransitions(statesModelDatas);

                // rerender
                visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v));

                OnPendingChildrenApplied?.Invoke(statesModelDatas ?? new List<StateModelData>());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OnPendingChildrenApplied handler failed: " + ex.Message);
            }
        }

        void UpdateIndexesRecursive(StateModelData state)
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

        /// <summary>Vrátí next index (int) pre daného parenta (null => top-level).</summary>
        private int GetNextIndexForParent(StateModelData? state)
        {
            IEnumerable<StateModelData> siblings;
            if (state == null)
            {
                siblings = statesModelDatas ?? Enumerable.Empty<StateModelData>();
            }
            else
            {
                //var parent = state.Parent;
                siblings = state.SubStatesData;
            }

            int max = 0;
            foreach (var s in siblings)
            {
                if (int.TryParse(s.Index, out var v))
                {
                    if (v > max) max = v;
                    continue;
                }
                //var seg = s.FullIndex?.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                //if (int.TryParse(seg, out var v2))
                //{
                //    if (v2 > max) max = v2;
                //}
            }
            return max + 1;
        }

        /// <summary>Odstráni state z jeho aktuálneho parenta (ak existuje). Vracia true ak sa odstránil.</summary>
        private bool RemoveStateFromItsParent(string fullIndex)
        {
            var top = statesModelDatas.FirstOrDefault(s => s.FullIndex == fullIndex);
            if (top != null)
            {
                statesModelDatas.Remove(top);
                return true;
            }

            var parent = FindParentStateOf(fullIndex, statesModelDatas);
            Debug.WriteLine("MAYBE HERE IT WILL WORK");
            if (parent != null && parent.SubStatesData != null)
            {
                Debug.WriteLine("DAS IT WORK");
                var child = parent.SubStatesData.FirstOrDefault(s => s.FullIndex == fullIndex);
                if (child != null)
                {
                    parent.SubStatesData.Remove(child);
                    Debug.WriteLine($"REMOVED {child.Name}-{child.FullIndex} from {parent.Name}-{parent.FullIndex}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Rekurzívne nastaví FullIndex a Index pre node + reindexuje jeho subtree (rekurzívne down to any depth).
        /// Deti dostanú nové lokálne indexy 1..N podľa poradia v kolekcii.
        /// </summary>
        private void UpdateFullIndexRecursive(StateModelData node, string newFullIndex)
        {
            if (node == null) return;

            node.FullIndex = newFullIndex;

            var last = newFullIndex.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? newFullIndex;
            node.Index = last;

            if (node.SubStatesData != null && node.SubStatesData.Count > 0)
            {
                for (int i = 0; i < node.SubStatesData.Count; i++)
                {
                    var child = node.SubStatesData[i];
                    var childFull = $"{newFullIndex}.{i + 1}";
                    UpdateFullIndexRecursive(child, childFull); // rekurzia—prehĺbenie do akýchkoľvek levelov
                }
            }
        }

        /// <summary>Aktualizuje všetky transitions (FromStage / NextStage) - exact match alebo prefix.</summary>
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
                        //else if (t.NextStateId.StartsWith(oldFull + "."))
                        //{
                        //    // preserve suffix
                        //    var suffix = t.NextStateId.Substring(oldFull.Length);
                        //    t.NextStateId = newFull + suffix;
                        //}
                        // NextState object reference will be re-resolved in PopulateTransitions, but keep it null for now
                        //t.NextState = null;
                    }
                }

                if (s.SubStatesData != null && s.SubStatesData.Count > 0)
                {
                    foreach (var sub in s.SubStatesData) walk(sub);
                }
            }

            foreach (var root in statesModelDatas) walk(root);
        }


        /// <summary>Vyhľadá parent StateModelData (ktorý má priamo child s daným fullIndex) rekurzívne.</summary>
        private static StateModelData? FindParentStateOf(string childFullIndex, IEnumerable<StateModelData> nodes)
        {
            foreach (var s in nodes)
            {
                if (s.SubStatesData.Any(x => x.FullIndex == childFullIndex))
                    return s;
                if (s.SubStatesData.Count > 0)
                {
                    var res = FindParentStateOf(childFullIndex, s.SubStatesData);
                    if (res != null) return res;
                }
            }
            return null;
        }
    }
}