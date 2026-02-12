using Amadevus.RecordGenerator;

namespace EOLib.Domain.Interact.Quest
{
    [Record]
    public sealed partial class GuildMemberInfo
    {
        public string Name { get; }

        public int Level { get; }

        public int LifetimeGuildPoints { get; }
    }
}
