using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Data
{
    internal class TransitionModelData : INotifyPropertyChanged
    {
        private string input;
        private string nextStage;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
