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

        public override PacketFamily Family => PacketFamily.Message;

        public override PacketAction Action => PacketAction.Open;

        public QuestStatusMessageHandler(IPlayerInfoProvider playerInfoProvider,
                                         IChatRepository chatRepository,
                                         IEnumerable<IStatusLabelNotifier> statusLabelNotifiers)
            : base(playerInfoProvider)
        {
            _chatRepository = chatRepository;
            _statusLabelNotifiers = statusLabelNotifiers;
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

            foreach (var notifier in _statusLabelNotifiers)
                notifier.ShowWarning(packet.Message);

            var chatData = new ChatData(ChatTab.System, string.Empty, packet.Message, ChatIcon.QuestMessage, ChatColor.Server);
            _chatRepository.AllChat[ChatTab.System].Add(chatData);

            return true;
        }
    }
}
