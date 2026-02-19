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

        public void RequestOpenInbox()
        {
            _packetSendService.SendPacket(new InboxOpenPacket());
        }

        public void SendBadgeSelection(int[] achievementIds)
        {
            _packetSendService.SendPacket(new BadgeSelectionPacket(achievementIds));
        }

        public void RequestBadgeData()
        {
            _packetSendService.SendPacket(new BadgeDataRequestPacket());
        }
    }

    public interface IAchievementActions
    {
        void RequestAchievements();

        void RequestLeaderboard(int achievementId);

        void RequestOpenInbox();

        void SendBadgeSelection(int[] achievementIds);

        void RequestBadgeData();
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

    /// <summary>
    /// Client→Server: EFFECT family, REPORT action (no payload).
    /// Server responds with LOCKER/OPEN packet containing delivery inbox items.
    /// </summary>
    public class InboxOpenPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Effect;
        public PacketAction Action => PacketAction.Report;

        public void Serialize(EoWriter writer) { }
        public void Deserialize(EoReader reader) { }
    }

    /// <summary>
    /// Client→Server: EFFECT family, AGREE action.
    /// Payload: comma-separated achievement IDs for badge selection.
    /// </summary>
    public class BadgeSelectionPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Effect;
        public PacketAction Action => PacketAction.Agree;

        public int[] AchievementIds { get; }

        public BadgeSelectionPacket() { AchievementIds = System.Array.Empty<int>(); }

        public BadgeSelectionPacket(int[] achievementIds)
        {
            AchievementIds = achievementIds;
        }

        public void Serialize(EoWriter writer)
        {
            var payload = string.Join(",", AchievementIds);
            foreach (var c in payload)
                writer.AddByte((byte)c);
        }

        public void Deserialize(EoReader reader) { }
    }

    /// <summary>
    /// Client→Server: EFFECT family, USE action (no payload).
    /// Server responds with [ACHBADGE] packets for all map characters.
    /// </summary>
    public class BadgeDataRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Effect;
        public PacketAction Action => PacketAction.Use;

        public void Serialize(EoWriter writer) { }
        public void Deserialize(EoReader reader) { }
    }
}
