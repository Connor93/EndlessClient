using Amadevus.RecordGenerator;

namespace EOLib.Domain.Interact.Quest
{
    public enum CustomBountyStatus
    {
        Open,
        Accepted
    }

    [Record]
    public sealed partial class CustomBountyData
    {
        public int Id { get; }

        public int ItemId { get; }

        public string ItemName { get; }

        public int Amount { get; }

        public string Poster { get; }

        public string AcceptedBy { get; }

        public CustomBountyStatus Status { get; }
    }
}
