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

        public ObservableCollection<TransitionViewModel> AllTransitions = new ObservableCollection<TransitionViewModel>();

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

                        foreach (var t in s.TransitionsData)
                        {
                            t.FromState = s;
                            t.NextState = FindStateByFullIndex(t.NextStateId);
                            Debug.WriteLine($"Transition - {s.Name} {s.FullIndex} -> {t.NextState.Name} {t.NextState.Name}");

                            AllTransitions.Add(new TransitionViewModel(t, s.FullIndex));

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

        // Delete prechod - odstráni z dát a z grafu
        public bool DeleteTransition(TransitionViewModel vm)
        {
            if (vm == null) return false;
            var t = vm.Transition;
            if (t == null) return false;

            var owner = FindStateByFullIndex(statesModelDatas, vm.Transition.FromState.FullIndex);
            if (owner != null && owner.TransitionsData != null)
            {
                var removed = owner.TransitionsData.Remove(t);
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

                    // odstrániť z vizuálu (grafu)
                    visualRender.RemoveTransition(t);
                    return true;
                }
            }

            return false;
        }

        // Add nový transition (cez UI): pridá do dát aj do grafu a do AllTransitions
        public bool AddTransition(string fromFullIndex, string input, StateModelData state)
        {
            if (string.IsNullOrEmpty(fromFullIndex) || string.IsNullOrEmpty(state.FullIndex)) return false;

            var owner = FindStateByFullIndex(statesModelDatas, fromFullIndex);
            if (owner == null) return false;

            var newT = new TransitionModelData
            {
                Input = input,
                NextState = state,
                FromState = owner
            };

            if (owner.TransitionsData == null)
                owner.TransitionsData = new ObservableCollection<TransitionModelData>();

            owner.TransitionsData.Add(newT);

            var vm = new TransitionViewModel(newT, owner.FullIndex);

            AllTransitions.Add(vm);
            visualRender.AddTransition(newT);

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

                    selectedVertex.State.Parent = parent;
                    parent.SubStatesData.Add(selectedVertex.State);
                    statesModelDatas.Remove(selectedVertex.State);
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

        public bool UpdateStateProperties(string fullIndex, string newName, string newIndex, string newOutput, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(fullIndex)) { errorMessage = "Missing state identifier."; return false; }

            var node = FindStateByFullIndex(fullIndex);
            if (node == null) { errorMessage = $"State '{fullIndex}' not found."; return false; }

            // trim newIndex
            newIndex = (newIndex ?? "").Trim();

            // get parent and siblings
            var parent = FindParentByFullIndex(node.FullIndex);
            IEnumerable<StateModelData> siblings = parent == null ? (statesModelDatas ?? Enumerable.Empty<StateModelData>()) : (parent.SubStatesData ?? Enumerable.Empty<StateModelData>());

            // check duplicate index among siblings (excluding current node)
            if (!string.IsNullOrEmpty(newIndex))
            {
                bool duplicate = siblings.Any(s => s != node && string.Equals(s.Index?.Trim(), newIndex, StringComparison.Ordinal));
                if (duplicate)
                {
                    errorMessage = $"Index '{newIndex}' already exists among siblings.";
                    return false;
                }
            }
            else
            {
                // don't allow empty index
                errorMessage = "Index (ID) cannot be empty.";
                return false;
            }

            // apply simple fields
            node.Name = newName ?? node.Name;
            node.Output = newOutput ?? node.Output;

            // if index changed -> recompute fullindex for this node and subtree, update transitions
            var oldFull = node.FullIndex;
            //if (!string.Equals(node.Index, newIndex, StringComparison.Ordinal))
            //{
            string newFull = parent == null ? newIndex : $"{parent.FullIndex}.{newIndex}";

            UpdateFullIndexRecursive(node, newFull);
            UpdateTransitionReferencesForMovedNode(oldFull, node.FullIndex);

            // repopulate transitions and re-render graph to ensure visuals sync
            PopulateTransitions(statesModelDatas);
            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v));
            //}
            //else
            //{
            //    // changed only name/output -> we still need to refresh transitions list UI and redraw maybe
            //    PopulateTransitions(statesModelDatas);
            //    visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v));
            //}

            return true;
        }
    }
}