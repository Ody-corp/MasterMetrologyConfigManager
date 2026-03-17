using MasterMetrology.Models.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MasterMetrology.Models.Visual
{
    internal class TransitionViewModel : INotifyPropertyChanged
    { 
        public TransitionModelData TransitionData { get; }
        public string DisplayText { get; }
        public string Id { get; }

        public TransitionViewModel(TransitionModelData transition, StateViewModel fromStateVm, StateViewModel nextStateVm)
        {
            TransitionData = transition;
            FromState = fromStateVm;
            NextState = nextStateVm;
            Id = $"{transition.FromState.FullIndex}_{transition.NextState.FullIndex}_{transition.Input}";
            DisplayText = $"{Short(transition.FromState.FullIndex)} → {Short(transition.NextState.FullIndex)}  Input:({transition.Input})";
        }

        public string Input
        {
            get => TransitionData.Input;
            set
            {
                if (TransitionData.Input == value) return;
                
                TransitionData.Input = value;
                OnPropertyChanged(nameof(Input));
                
            }
        }
        public string NextStateId
        {
            get => TransitionData.NextStateId;
            set
            {
                if (TransitionData.NextStateId == value) return;
                
                TransitionData.NextStateId = value;
                OnPropertyChanged(nameof(NextState));
                
            }
        }
        private StateViewModel _nextState;
        public StateViewModel NextState
        {
            get => _nextState;
            set
            {
                if (_nextState == value) return;
                
                _nextState = value;
                TransitionData.NextState = value.StateModel;
                OnPropertyChanged(nameof(NextState));
                
            }
        }
        private StateViewModel _fromState;
        public StateViewModel FromState
        {
            get => _fromState;
            set
            {
                if (_fromState == value) return;
                
                _fromState = value;
                TransitionData.FromState = value.StateModel;
                OnPropertyChanged(nameof(FromState));
                
            }
        }

        string Short(string s) => string.IsNullOrEmpty(s) ? "(?)" : s;

        public override string ToString() => DisplayText;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
