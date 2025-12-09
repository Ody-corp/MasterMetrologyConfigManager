using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MasterMetrology.Core.Rendering
{
    internal class OrthogonalRouter
    {
        public List<Point> Route(Point start, Point end)
        {
            var points = new List<Point> { start };

            if (start.X != end.X && start.Y != end.Y)
                points.Add(new Point(end.X, start.Y));

            points.Add(end);

            return SimplifyPath(points);
        }

        private List<Point> SimplifyPath(List<Point> points)
        {
            var simplified = new List<Point> { points[0] };

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = simplified.Last();
                var curr = points[i];
                var next = points[i + 1];

                if (!((prev.X == curr.X && curr.X == next.X) || (prev.Y == curr.Y && curr.Y == next.Y)))
                {
                    simplified.Add(curr);
                }  
            }

            simplified.Add(points.Last());
            return simplified;
        }
    }
}
