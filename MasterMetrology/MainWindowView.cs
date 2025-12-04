using MasterMetrology.Models.Data;
using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MasterMetrology
{
    /// <summary>
    /// Helper class to keep UI-related helper methods out of the main window
    /// </summary>
    internal class MainWindowView(ProcessController processController)
    {
        private readonly ProcessController _processController = processController;

        internal void SetCmbChild(GraphVertex selectedVertex, ComboBox cmbChild)
        {
            var flat = _processController.GetFlatStates();
            if (flat == null) { cmbChild.ItemsSource = null; return; }

            var selectedFull = selectedVertex?.State?.FullIndex; // uprav podľa tvojho selected objektu
            if (string.IsNullOrEmpty(selectedFull))
            {
                // ak nič nie je vybraté — zobrazíme len top-level položky (bez rodiča)
                var top = flat.Where(s => string.IsNullOrEmpty(GetParentFullIndex(s.FullIndex)))
                              .Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s })
                              .ToList();
                cmbChild.ItemsSource = top;
                cmbChild.DisplayMemberPath = "Display";
                cmbChild.SelectedValuePath = "Value";
                return;
            }

            var selParent = GetParentFullIndex(selectedFull);

            var options = flat.Where(s =>
            {
                // exclude self
                if (s.FullIndex == selectedFull) return false;

                // same parent => allowed (siblings)
                var p = GetParentFullIndex(s.FullIndex);
                return string.Equals(p, selParent, StringComparison.Ordinal);
            })
                .Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s })
                .ToList();

            cmbChild.ItemsSource = options;
            cmbChild.DisplayMemberPath = "Display";
            cmbChild.SelectedValuePath = "Value";
        }

        internal void SetLstChild(GraphVertex selectedVertex, ListBox lstChildren)
        {
            lstChildren.ItemsSource = selectedVertex.State.SubStatesData.Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s }).ToList();
            lstChildren.DisplayMemberPath = "Display";
            lstChildren.SelectedValuePath = "Value";
        }
        internal void SetCmbParent(GraphVertex selectedVertex, ComboBox cmbParent)
        {
            var flat = _processController.GetFlatStates();

            var candidates = flat
                .Where(s => s.FullIndex != selectedVertex.State.FullIndex)      
                .Where(s => !IsDescendant(selectedVertex.State, s))            
                .OrderBy(s => s.FullIndex)
                .ToList();

            var items = new List<object> { new { Display = "(none)", Value = "" } };
            items.AddRange(candidates.Select(s => new { Display = $"{s.FullIndex} - {s.Name}", Value = s }));

            cmbParent.ItemsSource = items;
            cmbParent.SelectedValuePath = "Value";
            cmbParent.DisplayMemberPath = "Display";

            var parent = _processController.FindParentByFullIndex(selectedVertex.State.FullIndex);
            cmbParent.SelectedValue = parent == null ? "" : parent;
        }



        public string GetParentFullIndex(string fullIndex)
        {
            if (string.IsNullOrEmpty(fullIndex))
            {
                return string.Empty;
            }

            var i = fullIndex.LastIndexOf('.');
            return i <= 0 ? string.Empty : fullIndex.Substring(0, i);
        }
        public bool IsDescendant(StateModelData ancestor, StateModelData possibleDescendant)
        {
            if (ancestor?.SubStatesData == null || ancestor.SubStatesData.Count == 0) return false;
            foreach (var child in ancestor.SubStatesData)
            {
                if (child.FullIndex == possibleDescendant.FullIndex) return true;
                if (IsDescendant(child, possibleDescendant)) return true;
            }
            return false;
        }
    }
}
