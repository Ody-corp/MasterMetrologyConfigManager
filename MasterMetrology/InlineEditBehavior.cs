using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterMetrology
{
    public static class InlineEditBehavior
    {
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(InlineEditBehavior),
                new PropertyMetadata(false, OnEnableChanged)
                );

        public static void SetEnable(DependencyObject element, bool value) => element.SetValue(EnableProperty, value);
        public static bool GetEnable(DependencyObject element) => (bool)element.GetValue(EnableProperty);

        private static string draftTextValue = "";
        private static MainWindowView mv;
        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;

            if ((bool)e.NewValue)
            {
                tb.PreviewMouseLeftButtonDown += Tb_PreviewMouseLeftButtonDown;
                tb.MouseDoubleClick += Tb_MouseDoubleClick;
                tb.LostKeyboardFocus += Tb_LostKeyboardFocus;
                tb.PreviewKeyDown += Tb_PreviewKeyDown;
            }
            else
            {
                tb.PreviewMouseLeftButtonDown -= Tb_PreviewMouseLeftButtonDown;
                tb.MouseDoubleClick -= Tb_MouseDoubleClick;
                tb.LostKeyboardFocus -= Tb_LostKeyboardFocus;
                tb.PreviewKeyDown -= Tb_PreviewKeyDown;
            }
        }

        // Single click => click line, if not in edit-mode, then click into TextBox
        private static void Tb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox tb) return;

            if (tb.IsReadOnly)
            {
                tb.Focusable = false;
            }
        }

        // Double click => start edit in the TextBox
        private static void Tb_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox tb) return;

            tb.IsReadOnly = false;
            tb.Focusable = true;
            tb.Focus();
            tb.SelectAll();

            draftTextValue = tb.Text;

            e.Handled = true;
        }

        // End Edit-mode
        private static void Tb_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            EndEdit(sender as TextBox);
        }

        private static void Tb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.IsReadOnly) return;

            if (e.Key == Key.Enter)
            {
                EndEdit(tb);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                tb.Text = draftTextValue;
                draftTextValue = "";
                    
                EndEdit(tb);
                e.Handled = true;
            }
        }

        private static void EndEdit(TextBox? tb)
        {
            if (tb == null) return;

            

            if (tb.Text.Trim().Length <= 0)
                tb.Text = draftTextValue.Trim();

            if (mv == null)
                mv = Window.GetWindow(tb).DataContext as MainWindowView;
                    
            if (tb.Name == "ID" && mv.CheckDuplicity())
            {
                tb.Text = draftTextValue;

                MessageBox.Show(
                    "This ID number already exists.",
                    "ID diplicity detected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }
            else
            {
                tb.IsReadOnly = true;
                tb.Focusable = false;
            }

            mv.CheckInNeedSort_Inputs();
            

            Keyboard.ClearFocus();
        }
    }
}
