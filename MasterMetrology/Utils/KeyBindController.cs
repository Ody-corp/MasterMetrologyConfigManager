using MasterMetrology.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MasterMetrology.Utils
{
    internal sealed class KeyBindController
    {
        private readonly Window _window;
        private readonly MainWindowView _vm;
        public KeyBindController(Window window, MainWindowView vm)
        {
            _window = window;
            _vm = vm;

            _window.PreviewKeyDown += OnKeyDown;

            // vráti fokus na window po zavretí MenuItem na topPanel
            _window.AddHandler(MenuItem.SubmenuClosedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    _window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Keyboard.ClearFocus();
                        _window.Focus(); 
                    }), DispatcherPriority.Input);
                }),
                true);
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;

            // CTRL+S => Save
            if (mods == ModifierKeys.Control 
                && e.Key == Key.S)
            {
                if (_vm.SaveCommand.CanExecute(null))
                {
                    _vm.SaveCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
            // CTRL+SHIFT+S => SaveAs
            if (mods.HasFlag(ModifierKeys.Control) 
                && mods.HasFlag(ModifierKeys.Shift) 
                && e.Key == Key.S)
            {
                if (_vm.SaveAsCommand.CanExecute(null))
                {
                    _vm.SaveAsCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
            // CTRL+N => New file
            if (mods == ModifierKeys.Control
                && e.Key == Key.N)
            {
                if (_vm.CreateNewFileCommand.CanExecute(null))
                {
                    _vm.CreateNewFileCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
            // CTRL+O => Import file
            if (mods == ModifierKeys.Control 
                && e.Key == Key.O)
            {
                if (_vm.ImportFileCommand.CanExecute(null))
                {
                    _vm.ImportFileCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
        }
    }
}
