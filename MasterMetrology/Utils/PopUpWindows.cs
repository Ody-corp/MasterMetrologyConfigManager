using MasterMetrology.Core.UI;
using System;
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
        /// <summary>
        /// Check changes are saved when closing app.
        /// </summary>
        /// <param name="_processController"></param>
        /// <returns></returns>
        public static bool ConfirmCloseIfDirty(ProcessController _processController)
        {
            if (!_processController.IsDirty) return true;

            var res = MessageBox.Show(
                "You have unsaved changes. Would you like to save them before exit?",
                "Unsaved changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning
            );

            if (res == MessageBoxResult.Cancel)
                return false;

            if (res == MessageBoxResult.No)
                return true;

            if (_processController.CanSave)
            {
                return _processController.Save();
            }
            else
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save as",
                    Filter = "XML files (*.xml)|*.xml",
                    FileName = "process.xml"
                };

                if (sfd.ShowDialog() == true)
                {
                    _processController.SaveAs(sfd.FileName);
                    return true; 
                }

                return false;
            }
        }

        internal enum ConfirmChangeResult
        {
            Apply,
            Discard,
            Cancel
        }

        internal static ConfirmChangeResult ConfirmStateSelectionIfDiff()
        {
            var res = MessageBox.Show(
                "Save changed data of state?",
                "Unsaved changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning
            );

            return res switch
            {
                MessageBoxResult.Yes => ConfirmChangeResult.Apply,
                MessageBoxResult.No => ConfirmChangeResult.Discard,
                _ => ConfirmChangeResult.Cancel,
            };
        }
    }
}
