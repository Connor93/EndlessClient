using System.Linq;
using AutomaticTypeMapper;
using EOLib.Domain.Interact.Quest;
using EOLib.Domain.Login;
using EOLib.Net.Handlers;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace EOLib.PacketHandlers.Quest
{
    [AutoMappedType]
    public class QuestListHandler : InGameOnlyPacketHandler<QuestListServerPacket>
    {
        private readonly IQuestDataRepository _questDataRepository;
        private readonly IBountyDataRepository _bountyDataRepository;

        public override PacketFamily Family => PacketFamily.Quest;

        public override PacketAction Action => PacketAction.List;

        public QuestListHandler(IPlayerInfoProvider playerInfoProvider,
                                IQuestDataRepository questDataRepository,
                                IBountyDataRepository bountyDataRepository)
            : base(playerInfoProvider)
        {
            _questDataRepository = questDataRepository;
            _bountyDataRepository = bountyDataRepository;
        }

        public override bool HandlePacket(QuestListServerPacket packet)
        {
            switch (packet.Page)
            {
                case QuestPage.Progress:
                    var allEntries = ((QuestListServerPacket.PageDataProgress)packet.PageData)
                        .QuestProgressEntries;

                    _questDataRepository.QuestProgress = allEntries
                        .Where(x => x.Description != "BOUNTY" && x.Description != "GUILDINFO")
                        .Select(x => new QuestProgressData.Builder
                        {
                            Name = x.Name,
                            Description = x.Description,
                            Icon = x.Icon,
                            Progress = x.Progress,
                            Target = x.Target,
                        }.ToImmutable())
                        .ToList();

                    _bountyDataRepository.Bounties = allEntries
                        .Where(x => x.Description == "BOUNTY")
                        .Select(x => new BountyProgressData.Builder
                        {
                            Name = x.Name,
                            Progress = x.Progress,
                            Target = x.Target,
                            Status = x.Progress >= x.Target ? BountyStatus.Complete : BountyStatus.InProgress,
                        }.ToImmutable())
                        .ToList();

                    var guildInfoEntry = allEntries.FirstOrDefault(x => x.Description == "GUILDINFO");
                    if (guildInfoEntry != null)
                    {
                        var parts = guildInfoEntry.Name.Split('|');
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
                    }
                    break;
                case QuestPage.History:
                    _questDataRepository.QuestHistory = ((QuestListServerPacket.PageDataHistory)packet.PageData).CompletedQuests;
                    break;
            }

            return true;
        }
    }
}

