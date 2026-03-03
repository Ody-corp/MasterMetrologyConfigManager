using System;
using System.Diagnostics;

namespace MasterMetrology.Utils
{
    internal static class PerfTimer
    {
        public const bool Enabled = true;

        public static long MeasureMs(Action action)
        {
            if (!Enabled) { action(); return 0; }
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        public static T MeasureMs<T>(Func<T> func, out long ms)
        {
            if (!Enabled) { ms = 0; return func(); }
            var sw = Stopwatch.StartNew();
            var res = func();
            sw.Stop();
            ms = sw.ElapsedMilliseconds;
            return res;
        }

        public static void Log(string label, long ms)
        {
            if (!Enabled) return;
            Debug.WriteLine($"[PERF] {label}: {ms} ms");
        }
    }
}