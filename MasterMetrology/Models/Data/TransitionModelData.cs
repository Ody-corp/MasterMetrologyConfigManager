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
        private string nextStage;
        private string fromStage;
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
        public string NextStage
        {
            get => nextStage;
            set
            {
                if (nextStage != value)
                {
                    nextStage = value;
                    OnPropertyChanged(nameof(NextStage));
                }
            }
        }
        public string FromStage
        {
            get => fromStage;
            set
            {
                if (fromStage != value)
                {
                    fromStage = value;
                    OnPropertyChanged(nameof(FromStage));
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
