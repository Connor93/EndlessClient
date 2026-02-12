using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Interact.Quest
{
    public interface IBountyDataProvider
    {
        IReadOnlyList<BountyProgressData> Bounties { get; }

        IReadOnlyList<CustomBountyData> CustomBounties { get; }

        GuildInfoData GuildInfo { get; }

        IReadOnlyList<GuildPerkData> GuildPerks { get; }

        IReadOnlyList<GuildBuffData> GuildBuffs { get; }

        IReadOnlyList<GuildMemberInfo> GuildMemberList { get; }
    }

    public interface IBountyDataRepository : IBountyDataProvider
    {
        new IReadOnlyList<BountyProgressData> Bounties { get; set; }

        new IReadOnlyList<CustomBountyData> CustomBounties { get; set; }

        new GuildInfoData GuildInfo { get; set; }

        new IReadOnlyList<GuildPerkData> GuildPerks { get; set; }

        new IReadOnlyList<GuildBuffData> GuildBuffs { get; set; }

        new IReadOnlyList<GuildMemberInfo> GuildMemberList { get; set; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class BountyDataRepository : IBountyDataRepository
    {
        public IReadOnlyList<BountyProgressData> Bounties { get; set; } = new List<BountyProgressData>();

        public IReadOnlyList<CustomBountyData> CustomBounties { get; set; } = new List<CustomBountyData>();

        public GuildInfoData GuildInfo { get; set; }

        public IReadOnlyList<GuildPerkData> GuildPerks { get; set; } = new List<GuildPerkData>();

        public IReadOnlyList<GuildBuffData> GuildBuffs { get; set; } = new List<GuildBuffData>();

        public IReadOnlyList<GuildMemberInfo> GuildMemberList { get; set; } = new List<GuildMemberInfo>();
    }
}

