using System.Windows;

namespace MasterMetrology.Core.History
{
    class TransitionSnapshot
    {
        public string Input { get; init; } = "";
        public string NextStateId { get; init; } = "";
        public List<Point> PathPoints { get; init; } = new List<Point>();
    }
}
