using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MasterMetrology.Models.Data
{
    public class TransitionModelData : INotifyPropertyChanged
    {
        private string input;
        private string nextStateId;
        private StateModelData nextState;
        private StateModelData fromState;
        private ObservableCollection<Point> pathPoints = new();

        public string Input
        {
            get => input;
            set
            {
                if (input != value)
                {
                    input = value;
                    OnPropertyChanged(nameof(Input));
                }
            }
        }
        public string NextStateId
        {
            get => nextStateId;
            set
            {
                if (nextStateId != value)
                {
                    nextStateId = value;
                    OnPropertyChanged(nameof(NextState));
                }
            }
        }
        public StateModelData NextState
        {
            get => nextState;
            set
            {
                if (nextState != value)
                {
                    nextState = value;
                    OnPropertyChanged(nameof(NextState));
                }
            }
        }
        public StateModelData FromState
        {
            get => fromState;
            set
            {
                if (fromState != value)
                {
                    fromState = value;
                    OnPropertyChanged(nameof(FromState));
                }
            }
        }
        
        public ObservableCollection<Point> PathPoints
        {
            get => pathPoints;
            set 
            { 
                pathPoints = value; 
                OnPropertyChanged(nameof(PathPoints)); 
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
