using System.Collections;
using System.Reflection;
using System.Windows;

namespace MasterMetrology.Utils
{
    public partial class ConfirmStateChangesWindow : Window
    {
        public string Message { get; }
        public string TxtApply { get; }
        public string TxtDiscard { get; }
        public string TxtCancel { get; }
        public bool? Result { get; private set; } // true=save, false=discard, null=cancel(X)

        public ConfirmStateChangesWindow(string title, string message, ArrayList value)
        {
            InitializeComponent();

            Title = title;
            Message = message;
            TxtApply = value[0].ToString();
            TxtDiscard = value[1].ToString();
            TxtCancel = value[2].ToString();

            DataContext = this;

            ShowDialog();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
        }

        private void Discard_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            base.OnClosed(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}