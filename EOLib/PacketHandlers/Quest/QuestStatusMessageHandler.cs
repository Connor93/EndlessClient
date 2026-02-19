using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using EOLib.Domain.Achievement;
using EOLib.Domain.Character;
using EOLib.Domain.Chat;
using EOLib.Domain.Interact.Quest;
using EOLib.Domain.Login;
using EOLib.Domain.Map;
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
        private readonly IAchievementRepository _achievementRepository;
        private readonly ICurrentMapStateRepository _currentMapStateRepository;
        private readonly ICharacterRepository _characterRepository;

        public override PacketFamily Family => PacketFamily.Message;

        public override PacketAction Action => PacketAction.Open;

        public QuestStatusMessageHandler(IPlayerInfoProvider playerInfoProvider,
                                         IChatRepository chatRepository,
                                         IEnumerable<IStatusLabelNotifier> statusLabelNotifiers,
                                         IBountyDataRepository bountyDataRepository,
                                         IAchievementRepository achievementRepository,
                                         ICurrentMapStateRepository currentMapStateRepository,
                                         ICharacterRepository characterRepository)
            : base(playerInfoProvider)
        {
            _chatRepository = chatRepository;
            _statusLabelNotifiers = statusLabelNotifiers;
            _bountyDataRepository = bountyDataRepository;
            _achievementRepository = achievementRepository;
            _currentMapStateRepository = currentMapStateRepository;
            _characterRepository = characterRepository;
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
                        if (fields.Length >= 7)
                        {
                            var name = fields[0];
                            var displayName = fields[1];
                            var isUnlocked = fields[2] == "1";
                            var isActive = fields[3] == "1";
                            var upkeepGold = int.TryParse(fields[fields.Length - 1], out var ug) ? ug : 0;
                            var upkeepPts = int.TryParse(fields[fields.Length - 2], out var up) ? up : 0;

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

            const string achievementsPrefix = "[ACHIEVEMENTS]";
            if (packet.Message.StartsWith(achievementsPrefix))
            {
                var payload = packet.Message.Substring(achievementsPrefix.Length);
                var achievements = new List<AchievementDefinition>();

                if (!string.IsNullOrEmpty(payload))
                {
                    var entries = payload.Split(';');
                    foreach (var entry in entries)
                    {
                        var fields = entry.Split(',');
                        // Format: id,name,description,type,target,tier_count,
                        //   t1_thresh:t1_exp:t1_itemid:t1_itemamt, ...,
                        //   current_progress,current_tier,unique_count,
                        //   players_t1,players_t2,...
                        if (fields.Length < 6) continue;

                        int.TryParse(fields[0], out var achId);
                        var achName = fields[1];
                        var achDesc = fields[2].Replace('~', ',');
                        var achType = fields[3];
                        int.TryParse(fields[4], out var achTarget);
                        int.TryParse(fields[5], out var tierCount);

                        var tiers = new List<AchievementTierData>();
                        int fi = 6;
                        for (int t = 0; t < tierCount && fi < fields.Length; t++, fi++)
                        {
                            var tierParts = fields[fi].Split(':');
                            int threshold = 0, expReward = 0, itemId = 0, itemAmount = 0;
                            if (tierParts.Length >= 1) int.TryParse(tierParts[0], out threshold);
                            if (tierParts.Length >= 2) int.TryParse(tierParts[1], out expReward);
                            if (tierParts.Length >= 3) int.TryParse(tierParts[2], out itemId);
                            if (tierParts.Length >= 4) int.TryParse(tierParts[3], out itemAmount);
                            tiers.Add(new AchievementTierData(threshold, expReward, itemId, itemAmount, 0));
                        }

                        int currentProgress = 0, currentTier = 0, uniqueCount = 0;
                        if (fi < fields.Length) int.TryParse(fields[fi++], out currentProgress);
                        if (fi < fields.Length) int.TryParse(fields[fi++], out currentTier);
                        if (fi < fields.Length) int.TryParse(fields[fi++], out uniqueCount);

                        for (int t = 0; t < tiers.Count && fi < fields.Length; t++, fi++)
                        {
                            int.TryParse(fields[fi], out var playersCompleted);
                            var old = tiers[t];
                            tiers[t] = new AchievementTierData(old.Threshold, old.ExpReward, old.ItemId, old.ItemAmount, playersCompleted);
                        }

                        achievements.Add(new AchievementDefinition(
                            achId, achName, achDesc, achType, achTarget,
                            tiers.ToArray(), currentProgress, currentTier, uniqueCount));
                    }
                }

                _achievementRepository.Achievements = achievements;
                return true;
            }

            const string achLeaderboardPrefix = "[ACHLEADERBOARD]";
            if (packet.Message.StartsWith(achLeaderboardPrefix))
            {
                var payload = packet.Message.Substring(achLeaderboardPrefix.Length);
                var entries = new List<LeaderboardEntry>();
                var parts = payload.Split(',');

                int achId = 0;
                if (parts.Length >= 1) int.TryParse(parts[0], out achId);

                for (int i = 1; i < parts.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(parts[i])) continue;
                    var sub = parts[i].Split(':');
                    var name = sub.Length >= 1 ? sub[0] : "";
                    int tier = 0, progress = 0;
                    if (sub.Length >= 2) int.TryParse(sub[1], out tier);
                    if (sub.Length >= 3) int.TryParse(sub[2], out progress);
                    if (!string.IsNullOrEmpty(name))
                        entries.Add(new LeaderboardEntry(name, tier, progress));
                }

                _achievementRepository.LeaderboardAchievementId = achId;
                _achievementRepository.LeaderboardEntries = entries;
                return true;
            }

            // [ACHBADGE]playerid,name1,name2,name3 — badge data for a character
            const string achBadgePrefix = "[ACHBADGE]";
            if (packet.Message.StartsWith(achBadgePrefix))
            {
                var payload = packet.Message.Substring(achBadgePrefix.Length);
                var parts = payload.Split(',');
                if (parts.Length >= 1 && int.TryParse(parts[0], out var playerId))
                {
                    var badgeNames = parts.Skip(1).Where(s => !string.IsNullOrEmpty(s)).ToArray();


                    // Update main character if it's us
                    if (_characterRepository.MainCharacter.ID == playerId)
                    {
                        _characterRepository.MainCharacter = _characterRepository.MainCharacter
                            .WithBadgeNames(badgeNames);

                    }
                    // Update map character
                    else if (_currentMapStateRepository.Characters.ContainsKey(playerId))
                    {
                        var oldChar = _currentMapStateRepository.Characters[playerId];
                        var newChar = oldChar.WithBadgeNames(badgeNames);
                        _currentMapStateRepository.Characters.Update(oldChar, newChar);

                    }

                }
                return true;
            }

            // [ACHMAXED]id1,id2,... — which achievements the player has fully completed
            const string achMaxedPrefix = "[ACHMAXED]";
            if (packet.Message.StartsWith(achMaxedPrefix))
            {
                var payload = packet.Message.Substring(achMaxedPrefix.Length);
                var ids = new List<int>();
                if (!string.IsNullOrEmpty(payload))
                {
                    foreach (var part in payload.Split(','))
                    {
                        if (int.TryParse(part, out var id))
                            ids.Add(id);
                    }
                }
                _achievementRepository.MaxedAchievementIds = ids;
                return true;
            }

            // [ACHSELECTED]id1,id2,id3 — currently selected badge IDs
            const string achSelectedPrefix = "[ACHSELECTED]";
            if (packet.Message.StartsWith(achSelectedPrefix))
            {
                var payload = packet.Message.Substring(achSelectedPrefix.Length);
                var ids = new List<int>();
                if (!string.IsNullOrEmpty(payload))
                {
                    foreach (var part in payload.Split(','))
                    {
                        if (int.TryParse(part, out var id))
                            ids.Add(id);
                    }
                }
                _achievementRepository.SelectedBadgeIds = ids;
                return true;
            }

            // [ACHBADGESELOK] — badge selection saved confirmation
            const string achBadgeSelOkPrefix = "[ACHBADGESELOK]";
            if (packet.Message.StartsWith(achBadgeSelOkPrefix))
            {
                // Selection was saved — no UI action needed, data already updated from broadcast
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
