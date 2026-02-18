using AutomaticTypeMapper;
using EOLib.Net.Communication;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Domain.Achievement
{
    [AutoMappedType]
    public class AchievementActions : IAchievementActions
    {
        private readonly IPacketSendService _packetSendService;

        public AchievementActions(IPacketSendService packetSendService)
        {
            _packetSendService = packetSendService;
        }

        public void RequestAchievements()
        {
            _packetSendService.SendPacket(new AchievementRequestPacket());
        }

        public void RequestLeaderboard(int achievementId)
        {
            _packetSendService.SendPacket(new AchievementLeaderboardRequestPacket(achievementId, 0));
        }
    }

    public interface IAchievementActions
    {
        void RequestAchievements();

        void RequestLeaderboard(int achievementId);
    }

    /// <summary>
    /// Client→Server: EFFECT family, REQUEST action (no payload).
    /// Server responds with [ACHIEVEMENTS] prefixed MESSAGE/OPEN packet.
    /// </summary>
    public class AchievementRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Effect;
        public PacketAction Action => PacketAction.Request;

        public void Serialize(EoWriter writer) { }
        public void Deserialize(EoReader reader) { }
    }

    /// <summary>
    /// Client→Server: EFFECT family, LIST action.
    /// Payload: short achievementId, char tier.
    /// Server responds with [ACHLEADERBOARD] prefixed MESSAGE/OPEN packet.
    /// </summary>
    public class AchievementLeaderboardRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Effect;
        public PacketAction Action => PacketAction.List;

        public int AchievementId { get; }
        public int Tier { get; }

        public AchievementLeaderboardRequestPacket() { }

        public AchievementLeaderboardRequestPacket(int achievementId, int tier)
        {
            AchievementId = achievementId;
            Tier = tier;
        }

        public void Serialize(EoWriter writer)
        {
            writer.AddShort(AchievementId);
            writer.AddChar(Tier);
        }

        public void Deserialize(EoReader reader)
        {
            // Client-to-server only, no deserialization needed
        }
    }
}
