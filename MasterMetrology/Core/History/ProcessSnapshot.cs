
namespace MasterMetrology.Core.History
{
    class ProcessSnapshot
    {
        public List<InputSnapshot> Inputs { get; init; } = new List<InputSnapshot>();
        public List<OutputSnapshot> Outputs { get; init; } = new List<OutputSnapshot>();
        public List<StateSnapshot> Roots { get; init; } = new List<StateSnapshot>();
        public string? SelectedFullIndex { get; init; }
        public bool IsDirty { get; init; }
        public string StateSignature { get; init; } = "";
        public string Signature { get; init; } = "";
    }
}
