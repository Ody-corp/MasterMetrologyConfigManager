using MasterMetrology.Models.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MasterMetrology.Models.Visual
{
    internal class StateViewModel : INotifyPropertyChanged
    {
        public StateModelData StateModel { get; }
        private StateViewModel _parent;

        public StateViewModel(StateModelData model)
        {
            StateModel = model;
            SubStates = new ObservableCollection<StateViewModel>();
        }

        public string Name
        {
            get => StateModel.Name;
            set
            {
                if (StateModel.Name == value) return;
                
                StateModel.Name = value;
                OnPropertyChanged(nameof(Name));
                
            }
        }

        public string Index
        {
            get => StateModel.Index;
            set
            {
                if (StateModel.Index == value) return;
                
                StateModel.Index = value;
                OnPropertyChanged(nameof(Index));
                
            }
        }

        public string FullIndex
        {
            get => StateModel.FullIndex;
            set
            {
                if (StateModel.FullIndex == value) return;
                
                StateModel.FullIndex = value;
                OnPropertyChanged(nameof(FullIndex));
                
            }
        }

        public string Output
        {
            get => StateModel.Output;
            set
            {
                if (StateModel.Output == value) return;
                
                StateModel.Output = value;
                OnPropertyChanged(nameof(Output));
                
            }
        }
        public StateViewModel Parent 
        { 
            get => _parent; 
            set
            {
                if (_parent == value) return;
                
                _parent = value;
                StateModel.Parent = value?.StateModel;
                OnPropertyChanged(nameof(Parent));
                
            }
        }
        public ObservableCollection<StateViewModel> SubStates { get; }

        //UI-only flags
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
        private bool _isDraftAdded = false;
        public bool IsDraftAdded
        {
            get => _isDraftAdded;
            set 
            { 
                if (_isDraftAdded == value) return; 
                _isDraftAdded = value; 
                OnPropertyChanged(); 
            }
        }
        private bool _isDraftRemoved = false;
        public bool IsDraftRemoved
        {
            get => _isDraftRemoved;
            set 
            { 
                if (_isDraftRemoved == value) return; 
                _isDraftRemoved = value; 
                OnPropertyChanged(); 
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
