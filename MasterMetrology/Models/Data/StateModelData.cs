using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Data
{
    public class StateModelData : INotifyPropertyChanged
    {
        private string name;
        private string index;
        private string fullIndex;
        private string output;
        private StateModelData parent = null;
        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Index
        {
            get => index;
            set
            {
                if (index != value)
                {
                    index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        public string FullIndex
        {
            get => fullIndex;
            set
            {
                if (fullIndex != value)
                {
                    fullIndex = value;
                    OnPropertyChanged(nameof(FullIndex));
                }
            }
        }

        public string Output
        {
            get => output;
            set
            {
                if (output != value)
                {
                    output = value;
                    OnPropertyChanged(nameof(Output));
                }
            }
        }
        public StateModelData Parent
        {
            get => parent;
            set
            {
                if (parent != value)
                {
                    parent = value;
                    OnPropertyChanged(nameof(Parent));
                }
            }
        }

        public ObservableCollection<StateModelData> SubStatesData { get; set; } = new();
        public ObservableCollection<TransitionModelData> TransitionsData { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
