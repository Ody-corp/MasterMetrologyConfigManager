
namespace MasterMetrology.Core.History
{
    class StateSnapshot
    {
        public string Name { get; init; } = "";
        public string Index { get; init; } = "";
        public string FullIndex { get; init; } = "";
        public string Output { get; init; } = "";
        public List<StateSnapshot> Children { get; init; } = new List<StateSnapshot>();
        public List<TransitionSnapshot> Transitions { get; init; } = new List<TransitionSnapshot>();
    }
}
