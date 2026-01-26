using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Utils
{
    class FullIndexComparer : IComparer<string>
    {
        public static readonly FullIndexComparer Instance = new FullIndexComparer();

        public int Compare(string a, string b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            var sa = a.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var sb = b.Split('.', StringSplitOptions.RemoveEmptyEntries);

            int n = Math.Min(sa.Length, sb.Length);
            for (int i = 0; i < n; i++)
            {
                int ia = TryParseInt(sa[i]);
                int ib = TryParseInt(sb[i]);

                int c = ia.CompareTo(ib);
                if (c != 0) return c;
            }

            // kratší prefix ide skôr: "1.2" < "1.2.1"
            return sa.Length.CompareTo(sb.Length);
        }

        private int TryParseInt(string s)
        {
            if (int.TryParse(s, out var v)) return v;
            return int.MaxValue; // ak by tam bolo niečo divné, hoď na koniec
        }
    }
}
