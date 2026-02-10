using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.Chat;
using EOLib.Domain.Interact.Quest;
using EOLib.Domain.Login;
using EOLib.Net.Handlers;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace EOLib.PacketHandlers.Quest
{
    [AutoMappedType]
    public class QuestStatusMessageHandler : InGameOnlyPacketHandler<MessageOpenServerPacket>
    {
        private readonly IChatRepository _chatRepository;
        private readonly IEnumerable<IStatusLabelNotifier> _statusLabelNotifiers;
        private readonly IBountyDataRepository _bountyDataRepository;

        public override PacketFamily Family => PacketFamily.Message;

        public override PacketAction Action => PacketAction.Open;

        public QuestStatusMessageHandler(IPlayerInfoProvider playerInfoProvider,
                                         IChatRepository chatRepository,
                                         IEnumerable<IStatusLabelNotifier> statusLabelNotifiers,
                                         IBountyDataRepository bountyDataRepository)
            : base(playerInfoProvider)
        {
            _chatRepository = chatRepository;
            _statusLabelNotifiers = statusLabelNotifiers;
            _bountyDataRepository = bountyDataRepository;
        }

        public override bool HandlePacket(MessageOpenServerPacket packet)
        {
            const string bountyPrefix = "[BOUNTY]";

            if (packet.Message.StartsWith(bountyPrefix))
            {
                // Parse: [BOUNTY]PlayerName|BountyName|GuildPoints
                var payload = packet.Message.Substring(bountyPrefix.Length);
                var parts = payload.Split('|');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var guildPoints))
                {
                    foreach (var notifier in _statusLabelNotifiers)
                        notifier.NotifyGuildBounty(parts[0], parts[1], guildPoints);
                }
                return true;
            }

            const string guildInfoPrefix = "[GUILDINFO]";
            if (packet.Message.StartsWith(guildInfoPrefix))
            {
                var payload = packet.Message.Substring(guildInfoPrefix.Length);
                var parts = payload.Split('|');
                if (parts.Length >= 7)
                {
                    _bountyDataRepository.GuildInfo = new GuildInfoData.Builder
                    {
                        GuildName = parts[0],
                        GuildTag = parts[1],
                        Level = int.TryParse(parts[2], out var lv) ? lv : 0,
                        Exp = int.TryParse(parts[3], out var exp) ? exp : 0,
                        ExpToNext = int.TryParse(parts[4], out var next) ? next : 0,
                        Points = int.TryParse(parts[5], out var pts) ? pts : 0,
                        Contribution = int.TryParse(parts[6], out var cont) ? cont : 0,
                        ActiveBuffs = parts.Length >= 8 ? parts[7] : string.Empty,
                    }.ToImmutable();
                }
                return true;
            }

            foreach (var notifier in _statusLabelNotifiers)
                notifier.ShowWarning(packet.Message);

            var chatData = new ChatData(ChatTab.System, string.Empty, packet.Message, ChatIcon.QuestMessage, ChatColor.Server);
            _chatRepository.AllChat[ChatTab.System].Add(chatData);

            return true;
        }
    }
}
