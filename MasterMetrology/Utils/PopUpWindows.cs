using MasterMetrology.Core.UI;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MasterMetrology.Utils
{
    internal static class PopUpWindows
    {
        internal enum ConfirmChangeResult
        {
            Apply,
            Discard,
            Cancel
        }        

        internal static ConfirmChangeResult DialogWindow(string title, string message, ArrayList buttons)
        {
            ConfirmStateChangesWindow res = new ConfirmStateChangesWindow(title, message, buttons) { Owner = Application.Current.MainWindow };

            return res.Result switch
            {
                true => ConfirmChangeResult.Apply,
                false => ConfirmChangeResult.Discard,
                _ => ConfirmChangeResult.Cancel
            };
        }
    }
}
