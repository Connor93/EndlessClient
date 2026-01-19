using System;
using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.Interact;
using EOLib.IO.Repositories;

namespace EOLib.Domain.Chat.Commands
{
    [AutoMappedType]
    public class NpcCommand : IPlayerCommand
    {
        private readonly IENFFileProvider _enfFileProvider;
        private readonly INpcInfoDialogActions _dialogActions;
        private readonly IChatRepository _chatRepository;

        public string CommandText => "npc";

        public NpcCommand(IENFFileProvider enfFileProvider,
                          INpcInfoDialogActions dialogActions,
                          IChatRepository chatRepository)
        {
            _enfFileProvider = enfFileProvider;
            _dialogActions = dialogActions;
            _chatRepository = chatRepository;
        }

        public bool Execute(string parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                ShowUsageMessage();
                return true;
            }

            var searchTerm = parameter.Trim();
            var enfFile = _enfFileProvider.ENFFile;

            // Try to parse as NPC ID first
            if (int.TryParse(searchTerm, out var npcId))
            {
                if (npcId > 0 && npcId < enfFile.Length)
                {
                    var npc = enfFile[npcId];
                    if (!string.IsNullOrEmpty(npc.Name))
                    {
                        _dialogActions.ShowNpcInfo(npcId);
                        return true;
                    }
                }
                _dialogActions.ShowNoNpcsFound(searchTerm);
                return true;
            }

            // Search by name (case-insensitive, partial match)
            var matchingNpcs = new List<int>();
            for (int i = 1; i < enfFile.Length; i++)
            {
                var npc = enfFile[i];
                if (!string.IsNullOrEmpty(npc.Name) &&
                    npc.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchingNpcs.Add(i);
                }
            }

            if (matchingNpcs.Count == 0)
            {
                _dialogActions.ShowNoNpcsFound(searchTerm);
            }
            else if (matchingNpcs.Count == 1)
            {
                _dialogActions.ShowNpcInfo(matchingNpcs[0]);
            }
            else
            {
                // Multiple matches - show results list
                _dialogActions.ShowNpcSearchResults(matchingNpcs.ToArray());
            }

            return true;
        }

        private void ShowUsageMessage()
        {
            var chatData = new ChatData(ChatTab.Local, "System", "Usage: #npc <name or id>", ChatIcon.LookingDude);
            _chatRepository.AllChat[ChatTab.Local].Add(chatData);
        }
    }
}
