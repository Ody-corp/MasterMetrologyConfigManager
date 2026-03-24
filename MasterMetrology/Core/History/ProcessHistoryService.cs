using MasterMetrology.Models.Data;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;

namespace MasterMetrology.Core.History
{
    internal class ProcessHistoryService
    {
        private readonly int _maxUndoRedoSteps;

        private readonly List<ProcessSnapshot> _undoHistory = new List<ProcessSnapshot>();
        private readonly List<ProcessSnapshot> _redoHistory = new List<ProcessSnapshot>();
        private ProcessSnapshot? _currentSnapshot;
        private int _trackingSuspendDepth;

        public event Action? HistoryStateChanged;

        public ProcessHistoryService(int maxUndoRedoSteps = 100)
        {
            _maxUndoRedoSteps = Math.Max(1, maxUndoRedoSteps);
        }

        public bool CanUndo => _undoHistory.Count > 0;
        public bool CanRedo => _redoHistory.Count > 0;
        public bool IsTrackingSuspended => _trackingSuspendDepth > 0;
        public ProcessSnapshot? CurrentSnapshot => _currentSnapshot;

        public IDisposable SuspendTracking()
        {
            _trackingSuspendDepth++;
            return new TrackingScope(this);
        }

        public void RegisterMutation(Func<bool, ProcessSnapshot> captureSnapshot)
        {
            if (IsTrackingSuspended)
                return;

            var mutatedSnapshot = captureSnapshot(true);

            if (_currentSnapshot != null &&
                _currentSnapshot.Signature == mutatedSnapshot.Signature)
            {
                return;
            }

            if (_currentSnapshot != null)
                PushWithLimit(_undoHistory, _currentSnapshot);

            _currentSnapshot = mutatedSnapshot;
            _redoHistory.Clear();
            RaiseHistoryStateChanged();
        }

        public void Initialize(Func<bool, ProcessSnapshot> captureSnapshot, bool clearStacks, bool isDirty)
        {
            _currentSnapshot = captureSnapshot(isDirty);

            if (clearStacks)
            {
                _undoHistory.Clear();
                _redoHistory.Clear();
            }

            RaiseHistoryStateChanged();
        }

        public bool Undo(Func<bool, ProcessSnapshot> captureSnapshot, Action<ProcessSnapshot> restoreSnapshot)
        {
            if (!CanUndo)
                return false;

            var snapshot = PopLast(_undoHistory);
            if (_currentSnapshot != null)
                PushWithLimit(_redoHistory, _currentSnapshot);

            using (SuspendTracking())
            {
                restoreSnapshot(snapshot);
            }

            _currentSnapshot = captureSnapshot(snapshot.IsDirty);
            RaiseHistoryStateChanged();
            return true;
        }

        public bool Redo(Func<bool, ProcessSnapshot> captureSnapshot, Action<ProcessSnapshot> restoreSnapshot)
        {
            if (!CanRedo)
                return false;

            var snapshot = PopLast(_redoHistory);
            if (_currentSnapshot != null)
                PushWithLimit(_undoHistory, _currentSnapshot);

            using (SuspendTracking())
            {
                restoreSnapshot(snapshot);
            }

            _currentSnapshot = captureSnapshot(snapshot.IsDirty);
            RaiseHistoryStateChanged();
            return true;
        }

        public ProcessSnapshot CaptureSnapshot(
            ObservableCollection<InputModelData> inputsDefModelDatas,
            ObservableCollection<OutputModelData> outputsDefModelDatas,
            List<StateModelData> statesModelDatas,
            string? selectedFullIndex,
            bool isDirty)
        {
            var inputs = inputsDefModelDatas
                .Select(i => new InputSnapshot
                {
                    Id = i.ID ?? "",
                    Name = i.Name ?? ""
                })
                .ToList();

            var outputs = outputsDefModelDatas
                .Select(o => new OutputSnapshot
                {
                    Id = o.ID ?? "",
                    Name = o.Name ?? "",
                    UpdateDefinition = o.UpdateDefinition,
                    UpdateParameters = o.UpdateParameters,
                    UpdateCalibration = o.UpdateCalibration,
                    UpdateMeasuredData = o.UpdateMeasuredData,
                    UpdateProcessedData = o.UpdateProcessedData
                })
                .ToList();

            var roots = statesModelDatas.Select(CaptureStateSnapshot).ToList();
            var stateSignature = BuildStateSignature(roots);
            var signature = BuildSignature(inputs, outputs, roots);

            return new ProcessSnapshot
            {
                Inputs = inputs,
                Outputs = outputs,
                Roots = roots,
                SelectedFullIndex = selectedFullIndex,
                IsDirty = isDirty,
                StateSignature = stateSignature,
                Signature = signature
            };
        }

        public string BuildStateSignatureFromStates(List<StateModelData> statesModelDatas)
        {
            var roots = statesModelDatas.Select(CaptureStateSnapshot).ToList();
            return BuildStateSignature(roots);
        }

        public List<StateModelData> RebuildStatesFromSnapshots(List<StateSnapshot> rootSnapshots)
        {
            var mapByFullIndex = new Dictionary<string, StateModelData>();
            var transitionsByState = new Dictionary<StateModelData, List<TransitionSnapshot>>();

            StateModelData CreateStateRecursive(StateSnapshot snapshot, StateModelData? parent)
            {
                var state = new StateModelData
                {
                    Name = snapshot.Name,
                    Index = snapshot.Index,
                    FullIndex = snapshot.FullIndex,
                    Output = snapshot.Output,
                    Parent = parent,
                    SubStatesData = new ObservableCollection<StateModelData>(),
                    TransitionsData = new ObservableCollection<TransitionModelData>()
                };

                if (!string.IsNullOrWhiteSpace(state.FullIndex) && !mapByFullIndex.ContainsKey(state.FullIndex))
                    mapByFullIndex.Add(state.FullIndex, state);

                transitionsByState[state] = snapshot.Transitions;

                foreach (var child in snapshot.Children)
                {
                    var childState = CreateStateRecursive(child, state);
                    state.SubStatesData.Add(childState);
                }

                return state;
            }

            var roots = new List<StateModelData>();
            foreach (var root in rootSnapshots)
                roots.Add(CreateStateRecursive(root, parent: null));

            foreach (var pair in transitionsByState)
            {
                var fromState = pair.Key;
                foreach (var transitionSnapshot in pair.Value)
                {
                    if (string.IsNullOrWhiteSpace(transitionSnapshot.NextStateId))
                        continue;

                    if (!mapByFullIndex.TryGetValue(transitionSnapshot.NextStateId, out var nextState))
                        continue;

                    fromState.TransitionsData.Add(new TransitionModelData
                    {
                        Input = transitionSnapshot.Input,
                        NextStateId = transitionSnapshot.NextStateId,
                        NextState = nextState,
                        FromState = fromState,
                        PathPoints = new ObservableCollection<Point>(transitionSnapshot.PathPoints)
                    });
                }
            }

            return roots;
        }

        private static ProcessSnapshot PopLast(List<ProcessSnapshot> list)
        {
            var idx = list.Count - 1;
            var item = list[idx];
            list.RemoveAt(idx);
            return item;
        }

        private void PushWithLimit(List<ProcessSnapshot> list, ProcessSnapshot snapshot)
        {
            list.Add(snapshot);
            if (list.Count > _maxUndoRedoSteps)
                list.RemoveAt(0);
        }

        private static StateSnapshot CaptureStateSnapshot(StateModelData state)
        {
            var snapshot = new StateSnapshot
            {
                Name = state.Name ?? "",
                Index = state.Index ?? "",
                FullIndex = state.FullIndex ?? "",
                Output = state.Output ?? "",
                Children = new List<StateSnapshot>(),
                Transitions = new List<TransitionSnapshot>()
            };

            foreach (var child in state.SubStatesData)
                snapshot.Children.Add(CaptureStateSnapshot(child));

            if (state.TransitionsData != null)
            {
                foreach (var transition in state.TransitionsData)
                {
                    snapshot.Transitions.Add(new TransitionSnapshot
                    {
                        Input = transition.Input ?? "",
                        NextStateId = transition.NextStateId ?? "",
                        PathPoints = transition.PathPoints?
                            .Select(p => new Point(p.X, p.Y))
                            .ToList() ?? new List<Point>()
                    });
                }
            }

            return snapshot;
        }

        private static string BuildSignature(
            List<InputSnapshot> inputs,
            List<OutputSnapshot> outputs,
            List<StateSnapshot> roots)
        {
            var sb = new StringBuilder(4096);

            AppendToken(sb, "I");
            AppendToken(sb, inputs.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var input in inputs)
            {
                AppendToken(sb, input.Id);
                AppendToken(sb, input.Name);
            }

            AppendToken(sb, "O");
            AppendToken(sb, outputs.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var output in outputs)
            {
                AppendToken(sb, output.Id);
                AppendToken(sb, output.Name);
                AppendToken(sb, output.UpdateDefinition ? "1" : "0");
                AppendToken(sb, output.UpdateParameters ? "1" : "0");
                AppendToken(sb, output.UpdateCalibration ? "1" : "0");
                AppendToken(sb, output.UpdateMeasuredData ? "1" : "0");
                AppendToken(sb, output.UpdateProcessedData ? "1" : "0");
            }

            AppendToken(sb, "S");
            AppendToken(sb, roots.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var root in roots)
                AppendStateSignature(sb, root);

            return sb.ToString();
        }

        private static string BuildStateSignature(List<StateSnapshot> roots)
        {
            var sb = new StringBuilder(2048);
            AppendToken(sb, roots.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var root in roots)
                AppendStateSignature(sb, root);
            return sb.ToString();
        }

        private static void AppendStateSignature(StringBuilder sb, StateSnapshot state)
        {
            AppendToken(sb, state.Name);
            AppendToken(sb, state.Index);
            AppendToken(sb, state.FullIndex);
            AppendToken(sb, state.Output);

            AppendToken(sb, state.Transitions.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var transition in state.Transitions)
            {
                AppendToken(sb, transition.Input);
                AppendToken(sb, transition.NextStateId);
                AppendToken(sb, transition.PathPoints.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var point in transition.PathPoints)
                {
                    AppendToken(sb, point.X.ToString("R", CultureInfo.InvariantCulture));
                    AppendToken(sb, point.Y.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            AppendToken(sb, state.Children.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var child in state.Children)
                AppendStateSignature(sb, child);
        }

        private static void AppendToken(StringBuilder sb, string value)
        {
            var safeValue = value ?? "";
            sb.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safeValue)
                .Append('|');
        }

        private void RaiseHistoryStateChanged() => HistoryStateChanged?.Invoke();

        private void ResumeTracking()
        {
            if (_trackingSuspendDepth > 0)
                _trackingSuspendDepth--;
        }

        private sealed class TrackingScope : IDisposable
        {
            private ProcessHistoryService? _owner;

            public TrackingScope(ProcessHistoryService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                    return;

                _owner.ResumeTracking();
                _owner = null;
            }
        }
    }
}
