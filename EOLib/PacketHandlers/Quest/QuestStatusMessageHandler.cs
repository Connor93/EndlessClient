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
                        Bank = parts.Length >= 9 && int.TryParse(parts[8], out var bank) ? bank : 0,
                        OnlineCount = parts.Length >= 10 && int.TryParse(parts[9], out var online) ? online : 0,
                    }.ToImmutable();
                }
                return true;
            }

            const string guildBoardPrefix = "[GUILDBOARD]";
            if (packet.Message.StartsWith(guildBoardPrefix))
            {
                var payload = packet.Message.Substring(guildBoardPrefix.Length);
                var bounties = new List<CustomBountyData>();

                if (!string.IsNullOrEmpty(payload))
                {
                    var entries = payload.Split(';');
                    foreach (var entry in entries)
                    {
                        var fields = entry.Split(',');
                        if (fields.Length >= 7)
                        {
                            bounties.Add(new CustomBountyData.Builder
                            {
                                Id = int.TryParse(fields[0], out var id) ? id : 0,
                                ItemId = int.TryParse(fields[1], out var itemId) ? itemId : 0,
                                Amount = int.TryParse(fields[2], out var amount) ? amount : 0,
                                Poster = fields[3],
                                AcceptedBy = fields[4],
                                Status = int.TryParse(fields[5], out var status) && status == 1
                                    ? CustomBountyStatus.Accepted
                                    : CustomBountyStatus.Open,
                                ItemName = fields[6],
                            }.ToImmutable());
                        }
                    }
                }

                _bountyDataRepository.CustomBounties = bounties;
                return true;
            }

            const string guildPerksPrefix = "[GUILDPERKS]";
            if (packet.Message.StartsWith(guildPerksPrefix))
            {
                var payload = packet.Message.Substring(guildPerksPrefix.Length);
                var perks = new List<GuildPerkData>();

                if (!string.IsNullOrEmpty(payload))
                {
                    var entries = payload.Split(';');
                    foreach (var entry in entries)
                    {
                        var fields = entry.Split(',');
                        if (fields.Length >= 6)
                        {
                            perks.Add(new GuildPerkData.Builder
                            {
                                Name = fields[0],
                                DisplayName = fields[1],
                                RequiredLevel = int.TryParse(fields[2], out var reqLv) ? reqLv : 0,
                                GoldCost = int.TryParse(fields[3], out var cost) ? cost : 0,
                                IsUnlocked = fields[4] == "1",
                                PerkIndex = int.TryParse(fields[5], out var idx) ? idx : 0,
                            }.ToImmutable());
                        }
                    }
                }

                _bountyDataRepository.GuildPerks = perks;
                return true;
            }

            const string guildBuffsPrefix = "[GUILDBUFFS]";
            if (packet.Message.StartsWith(guildBuffsPrefix))
            {
                var payload = packet.Message.Substring(guildBuffsPrefix.Length);
                var buffs = new List<GuildBuffData>();

                if (!string.IsNullOrEmpty(payload))
                {
                    var entries = payload.Split(';');
                    foreach (var entry in entries)
                    {
                        var fields = entry.Split(',');
                        // name,display,unlocked,active,stats(key:val pairs),upkeepPts,upkeepGold
                        // Stats field contains colons so we need at least 7+ fields
                        // but stats like "str:3, int:3" contains commas too
                        // The stats field uses colon-separated key:val pairs joined by ", "
                        // Format: name,display,unlocked,active,stat1:v1, stat2:v2, ...,upkeepPts,upkeepGold
                        // We know the last 2 fields are integers, and fields 2,3 are 0/1
                        // Parse from the ends
                        if (fields.Length >= 7)
                        {
                            var name = fields[0];
                            var displayName = fields[1];
                            var isUnlocked = fields[2] == "1";
                            var isActive = fields[3] == "1";
                            var upkeepGold = int.TryParse(fields[fields.Length - 1], out var ug) ? ug : 0;
                            var upkeepPts = int.TryParse(fields[fields.Length - 2], out var up) ? up : 0;

                            // Everything between index 4 and Length-3 is the stats description
                            var statParts = new List<string>();
                            for (int i = 4; i <= fields.Length - 3; i++)
                                statParts.Add(fields[i].Trim());
                            var statsDesc = string.Join(", ", statParts);

                            buffs.Add(new GuildBuffData.Builder
                            {
                                Name = name,
                                DisplayName = displayName,
                                IsUnlocked = isUnlocked,
                                IsActive = isActive,
                                StatsDescription = statsDesc,
                                UpkeepPoints = upkeepPts,
                                UpkeepGold = upkeepGold,
                            }.ToImmutable());
                        }
                    }
                }

                _bountyDataRepository.GuildBuffs = buffs;
                return true;
            }

            const string guildMembersPrefix = "[GUILDMEMBERS]";
            if (packet.Message.StartsWith(guildMembersPrefix))
            {
                var payload = packet.Message.Substring(guildMembersPrefix.Length);
                var members = new List<GuildMemberInfo>();

                if (!string.IsNullOrEmpty(payload))
                {
                    var entries = payload.Split(';');
                    foreach (var entry in entries)
                    {
                        var fields = entry.Split(',');
                        if (fields.Length >= 3)
                        {
                            members.Add(new GuildMemberInfo.Builder
                            {
                                Name = fields[0],
                                Level = int.TryParse(fields[1], out var lv) ? lv : 0,
                                LifetimeGuildPoints = int.TryParse(fields[2], out var gp) ? gp : 0,
                            }.ToImmutable());
                        }
                    }
                }

                _bountyDataRepository.GuildMemberList = members;
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
