using Amadevus.RecordGenerator;

namespace EOLib.Domain.Interact.Quest
{
    public enum BountyStatus
    {
        InProgress,
        Complete
    }

    [Record]
    public sealed partial class BountyProgressData
    {
        public string Name { get; }

        public int Progress { get; }

        public int Target { get; }

        public BountyStatus Status { get; }
    }
}
