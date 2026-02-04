using System.ComponentModel;
using System.Text.RegularExpressions;

namespace MasterMetrology.Models.Data
{
    internal class InputsDefModelData : INotifyPropertyChanged
    {
        private string id;
        private string name;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
