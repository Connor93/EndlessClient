using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Interact.Quest
{
    public interface IBountyDataProvider
    {
        IReadOnlyList<BountyProgressData> Bounties { get; }
    }

    public interface IBountyDataRepository : IBountyDataProvider
    {
        new IReadOnlyList<BountyProgressData> Bounties { get; set; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class BountyDataRepository : IBountyDataRepository
    {
        public IReadOnlyList<BountyProgressData> Bounties { get; set; } = new List<BountyProgressData>();
    }
}
