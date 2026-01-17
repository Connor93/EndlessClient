using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using EOLib.Domain.Interact;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;

namespace EOLib.Domain.Chat.Commands
{
    [AutoMappedType]
    public class ItemCommand : IPlayerCommand
    {
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IItemInfoDialogActions _dialogActions;
        private readonly IChatRepository _chatRepository;

        public string CommandText => "item";

        public ItemCommand(IEIFFileProvider eifFileProvider,
                           IItemInfoDialogActions dialogActions,
                           IChatRepository chatRepository)
        {
            _eifFileProvider = eifFileProvider;
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
            var eifFile = _eifFileProvider.EIFFile;

            // Try to parse as item ID first
            if (int.TryParse(searchTerm, out var itemId))
            {
                if (itemId > 0 && itemId < eifFile.Length)
                {
                    var item = eifFile[itemId];
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        _dialogActions.ShowItemInfo(itemId);
                        return true;
                    }
                }
                _dialogActions.ShowNoItemsFound(searchTerm);
                return true;
            }

            // Search by name (case-insensitive, partial match)
            var matchingItems = new List<int>();
            for (int i = 1; i < eifFile.Length; i++)
            {
                var item = eifFile[i];
                if (!string.IsNullOrEmpty(item.Name) &&
                    item.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchingItems.Add(i);
                }
            }

            if (matchingItems.Count == 0)
            {
                _dialogActions.ShowNoItemsFound(searchTerm);
            }
            else if (matchingItems.Count == 1)
            {
                _dialogActions.ShowItemInfo(matchingItems[0]);
            }
            else
            {
                // Multiple matches - show results list
                _dialogActions.ShowItemSearchResults(matchingItems.ToArray());
            }

            return true;
        }

        private void ShowUsageMessage()
        {
            var chatData = new ChatData(ChatTab.Local, "System", "Usage: #item <name or id>", ChatIcon.LookingDude);
            _chatRepository.AllChat[ChatTab.Local].Add(chatData);
        }
    }
}
