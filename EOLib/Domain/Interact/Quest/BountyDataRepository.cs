using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Interact.Quest
{
    public interface IBountyDataProvider
    {
        IReadOnlyList<BountyProgressData> Bounties { get; }

        GuildInfoData GuildInfo { get; }
    }

    public interface IBountyDataRepository : IBountyDataProvider
    {
        new IReadOnlyList<BountyProgressData> Bounties { get; set; }

        new GuildInfoData GuildInfo { get; set; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class BountyDataRepository : IBountyDataRepository
    {
        public IReadOnlyList<BountyProgressData> Bounties { get; set; } = new List<BountyProgressData>();

        public GuildInfoData GuildInfo { get; set; }
    }
}
