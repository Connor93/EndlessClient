using Amadevus.RecordGenerator;

namespace EOLib.Domain.Interact.Quest
{
    [Record]
    public sealed partial class GuildPerkData
    {
        public string Name { get; }

        public string DisplayName { get; }

        public int RequiredLevel { get; }

        public int GoldCost { get; }

        public bool IsUnlocked { get; }

        public int PerkIndex { get; }
    }
}
