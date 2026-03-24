
namespace MasterMetrology.Core.History
{
    class OutputSnapshot
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public bool UpdateDefinition { get; init; }
        public bool UpdateParameters { get; init; }
        public bool UpdateCalibration { get; init; }
        public bool UpdateMeasuredData { get; init; }
        public bool UpdateProcessedData { get; init; }
    }
}
