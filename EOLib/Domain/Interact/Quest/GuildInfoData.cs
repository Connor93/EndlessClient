using Amadevus.RecordGenerator;

namespace EOLib.Domain.Interact.Quest
{
    [Record]
    public sealed partial class GuildInfoData
    {
        public string GuildName { get; }

        public string GuildTag { get; }

        public int Level { get; }

        public int Exp { get; }

        public int ExpToNext { get; }

        public int Points { get; }

        public int Contribution { get; }

        public string ActiveBuffs { get; }

        public int Bank { get; }

        public int OnlineCount { get; }
    }
}
