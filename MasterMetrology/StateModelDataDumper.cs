using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MasterMetrology.Models.Data;

public static class StateModelDataDumper
{
    /// <summary>
    /// Vytvorí textový výpis stromu stavov (FullIndex, Index, Name, Output, pozícia, počet transitions) rekurzívne.
    /// </summary>
    /// <param name="states">Koreňová kolekcia stavov (top-level states)</param>
    /// <param name="writeToDebug">Ak true, tiež napíše výsledok do Debug outputu</param>
    /// <returns>String s celým dumpom</returns>
    public static string DumpStates(IEnumerable<StateModelData> states, bool writeToDebug = true)
    {
        if (states == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var s in states)
        {
            DumpStateRecursive(s, sb, 0);
        }
        var result = sb.ToString();
        if (writeToDebug) Debug.WriteLine(result);
        return result;
    }

    private static void DumpStateRecursive(StateModelData state, StringBuilder sb, int depth)
    {
        if (state == null) return;
        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}- FullIndex: {state.FullIndex ?? "<null>"}");
        sb.AppendLine($"{indent}  Index    : {state.Index ?? "<null>"}");
        sb.AppendLine($"{indent}  Name     : {state.Name ?? "<null>"}");
        sb.AppendLine($"{indent}  Parent   : {(state.Parent != null ? state.Parent.Name : "<null>")}");
        sb.AppendLine($"{indent}  Output   : {state.Output ?? "<null>"}");
        // transitions info (count + optional detail)
        int tcount = state.TransitionsData?.Count ?? 0;
        sb.AppendLine($"{indent}  Transitions: {tcount}");
        if (tcount > 0)
        {
            foreach (var t in state.TransitionsData)
            {
                // prispôsob podľa toho, aké polia má tvoj TransitionModelData (tu predpokladám FromStage/Input/NextStage)
                var display = GetTransitionDisplay(t);
                sb.AppendLine($"{indent}    - {display}");
            }
        }

        // Substates
        int scount = state.SubStatesData?.Count ?? 0;
        sb.AppendLine($"{indent}  SubStates : {scount}");
        if (scount > 0)
        {
            foreach (var sub in state.SubStatesData)
            {
                DumpStateRecursive(sub, sb, depth + 1);
            }
        }
    }

    private static string GetTransitionDisplay(object t)
    {
        // Ak máš konkrétny typ, nahraď 'object' a rozbal presné polia.
        // Príklad (ak TransitionModelData má Input, FromStage, NextStage):
        try
        {
            dynamic dt = t;
            string input = dt.Input ?? "";
            string from = dt.FromStage ?? "";
            string next = dt.NextStage ?? "";
            return $"Input='{input}', From='{from}', Next='{next}'";
        }
        catch
        {
            return t?.ToString() ?? "<null transition>";
        }
    }
}
