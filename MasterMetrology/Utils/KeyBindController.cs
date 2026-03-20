using MasterMetrology.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

            // vráti fokus po zavretí ContextMenu
            _window.AddHandler(ContextMenu.ClosedEvent,
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
            // CTRL+Z => Undo
            if (mods == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (ShouldRouteUndoToFocusedTextControl())
                    return;

                if (_vm.UndoCommand.CanExecute(null))
                {
                    _vm.UndoCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }

            // CTRL+Y or CTRL+SHIFT+Z => Redo
            if ((mods == ModifierKeys.Control && e.Key == Key.Y) ||
                (mods.HasFlag(ModifierKeys.Control) && mods.HasFlag(ModifierKeys.Shift) && e.Key == Key.Z))
            {
                if (ShouldRouteRedoToFocusedTextControl())
                    return;

                if (_vm.RedoCommand.CanExecute(null))
                {
                    _vm.RedoCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
            // DEL => Delete Selected State/InputDef/OutputDef/SubState/Transition
            if (e.Key == Key.Delete)
            {

                if (IsTypingIntoTextControl())
                    return;

                if (_vm.CanDeleteLastSelected())
                {
                    _vm.DeleteLastSelected();
                    e.Handled = true;
                }

                return;
            }
        }

        // -------- Helpers --------

        private static bool ShouldRouteUndoToFocusedTextControl()
        {
            if (Keyboard.FocusedElement is TextBoxBase tb)
                return tb.IsEnabled && !tb.IsReadOnly && tb.CanUndo;

            // PasswordBox does not expose undo stack; keep default text behavior.
            if (Keyboard.FocusedElement is PasswordBox pb)
                return pb.IsEnabled;

            return false;
        }

        private static bool ShouldRouteRedoToFocusedTextControl()
        {
            if (Keyboard.FocusedElement is TextBoxBase tb)
            {
                if (!tb.IsEnabled || tb.IsReadOnly)
                    return false;

                return ApplicationCommands.Redo.CanExecute(null, tb);
            }

            if (Keyboard.FocusedElement is PasswordBox pb)
                return pb.IsEnabled;

            return false;
        }

        private static bool IsTypingIntoTextControl()
        {
            if (Keyboard.FocusedElement is TextBox tb)
                return tb.IsEnabled && !tb.IsReadOnly;

            if (Keyboard.FocusedElement is RichTextBox rtb)
                return rtb.IsEnabled && !rtb.IsReadOnly;

            if (Keyboard.FocusedElement is PasswordBox pb)
                return pb.IsEnabled;

            return false;
        }
    }
}
