using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Dialogs.Factories;
using EOLib.Domain.Chat;
using EOLib.Domain.Interact;
using EOLib.IO.Repositories;
using EOLib.Net.Communication;
using EOLib.Net.Packets;
using Optional;

namespace EndlessClient.Dialogs.Actions
{
    [AutoMappedType]
    public class ItemInfoDialogActions : IItemInfoDialogActions
    {
        private readonly IItemInfoDialogFactory _dialogFactory;
        private readonly ICodeDrawnSearchResultsDialogFactory _searchResultsDialogFactory;
        private readonly IActiveDialogRepository _activeDialogRepository;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IChatRepository _chatRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IPacketSendService _packetSendService;

        public ItemInfoDialogActions(IItemInfoDialogFactory dialogFactory,
                                      ICodeDrawnSearchResultsDialogFactory searchResultsDialogFactory,
                                      IActiveDialogRepository activeDialogRepository,
                                      IEIFFileProvider eifFileProvider,
                                      IChatRepository chatRepository,
                                      ISfxPlayer sfxPlayer,
                                      IPacketSendService packetSendService)
        {
            _dialogFactory = dialogFactory;
            _searchResultsDialogFactory = searchResultsDialogFactory;
            _activeDialogRepository = activeDialogRepository;
            _eifFileProvider = eifFileProvider;
            _chatRepository = chatRepository;
            _sfxPlayer = sfxPlayer;
            _packetSendService = packetSendService;
        }

        public void ShowItemInfo(int itemId)
        {
            var eifFile = _eifFileProvider.EIFFile;
            if (itemId <= 0 || itemId >= eifFile.Length)
                return;

            var item = eifFile[itemId];
            if (string.IsNullOrEmpty(item.Name))
                return;

            // Close any existing item info dialog
            _activeDialogRepository.ItemInfoDialog.MatchSome(dlg =>
            {
                if (dlg is BaseEODialog baseDialog) baseDialog.Close();
                else if (dlg is CodeDrawnScrollingListDialog codeDrawn) codeDrawn.Close();
            });

            var dlg = _dialogFactory.Create(item);
            dlg.DialogClosed += (_, _) => _activeDialogRepository.ItemInfoDialog = Option.None<XNAControls.IXNADialog>();
            _activeDialogRepository.ItemInfoDialog = Option.Some(dlg);

            dlg.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            dlg.Show();

            // Request item source info from server
            System.Console.WriteLine($"[DEBUG] Sending item source request for item {itemId}");
            _packetSendService.SendPacketAsync(new ItemSourceRequestPacket { ItemId = itemId });
        }

        public void ShowNoItemsFound(string searchTerm)
        {
            var message = $"No items found matching '{searchTerm}'";
            var chatData = new ChatData(ChatTab.Local, "System", message, ChatIcon.LookingDude);
            _chatRepository.AllChat[ChatTab.Local].Add(chatData);
        }

        public void ShowItemSearchResults(int[] matchingItemIds)
        {
            var eifFile = _eifFileProvider.EIFFile;

            // Close any existing search results dialog
            _activeDialogRepository.SearchResultsDialog.MatchSome(dlg => dlg.Close());

            // Create a code-drawn search results dialog
            var dlg = _searchResultsDialogFactory.Create($"Found {matchingItemIds.Length} items");

            // Add clickable items to the list
            foreach (var id in matchingItemIds)
            {
                var itemRecord = eifFile[id];
                var capturedId = id;
                dlg.AddItem($"[{id}] {itemRecord.Name}", () => ShowItemInfo(capturedId));
            }

            dlg.DialogClosed += (_, _) => _activeDialogRepository.SearchResultsDialog = Option.None<CodeDrawnSearchResultsDialog>();
            _activeDialogRepository.SearchResultsDialog = Option.Some(dlg);

            dlg.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            dlg.Show();
        }
    }
}

