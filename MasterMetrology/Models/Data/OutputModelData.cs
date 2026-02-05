using System.ComponentModel;
using System.Text.RegularExpressions;

namespace MasterMetrology.Models.Data
{
    internal class OutputModelData : INotifyPropertyChanged
    {
        private string name;
        private string id;
        private bool updateDefinition;
        private bool updateParameters;
        private bool updateCalibration;
        private bool updateMeasuredData;
        private bool updateProcessedData;
        public string ID
        {
            get => id;
            set
            {
                if (!Regex.IsMatch(value, @"^\d*$"))
                    return;

                if (id != value)
                {
                    id = value;
                    OnPropertyChanged(nameof(ID));
                }
            }
        }
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
        public bool UpdateDefinition
        {
            get => updateDefinition;
            set
            {
                if (updateDefinition != value)
                {
                    updateDefinition = value;
                    OnPropertyChanged(nameof(UpdateDefinition));
                }
            }
        }
        public bool UpdateParameters
        {
            get => updateParameters;
            set
            {
                if (updateParameters != value)
                {
                    updateParameters = value;
                    OnPropertyChanged(nameof(UpdateParameters));
                }
            }
        }
        public bool UpdateCalibration
        {
            get => updateCalibration;
            set
            {
                if (updateCalibration != value)
                {
                    updateCalibration = value;
                    OnPropertyChanged(nameof(UpdateCalibration));
                }
            }
        }
        public bool UpdateMeasuredData
        {
            get => updateMeasuredData;
            set
            {
                if (updateMeasuredData != value)
                {
                    updateMeasuredData = value;
                    OnPropertyChanged(nameof(UpdateMeasuredData));
                }
            }
        }
        public bool UpdateProcessedData
        {
            get => updateProcessedData;
            set
            {
                if (updateProcessedData != value)
                {
                    updateProcessedData = value;
                    OnPropertyChanged(nameof(UpdateProcessedData));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }
}
