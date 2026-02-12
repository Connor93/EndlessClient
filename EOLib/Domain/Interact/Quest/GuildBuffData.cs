using Amadevus.RecordGenerator;

namespace EOLib.Domain.Interact.Quest
{
    [Record]
    public sealed partial class GuildBuffData
    {
        public string Name { get; }

        public string DisplayName { get; }

        public bool IsUnlocked { get; }

        public bool IsActive { get; }

        public string StatsDescription { get; }

        public int UpkeepPoints { get; }

        public int UpkeepGold { get; }
    }
}
