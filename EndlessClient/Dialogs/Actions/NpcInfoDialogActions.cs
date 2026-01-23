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
    public class NpcInfoDialogActions : INpcInfoDialogActions
    {
        private readonly INpcInfoDialogFactory _dialogFactory;
        private readonly ICodeDrawnSearchResultsDialogFactory _searchResultsDialogFactory;
        private readonly IActiveDialogRepository _activeDialogRepository;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IChatRepository _chatRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IPacketSendService _packetSendService;

        public NpcInfoDialogActions(INpcInfoDialogFactory dialogFactory,
                                    ICodeDrawnSearchResultsDialogFactory searchResultsDialogFactory,
                                    IActiveDialogRepository activeDialogRepository,
                                    IENFFileProvider enfFileProvider,
                                    IChatRepository chatRepository,
                                    ISfxPlayer sfxPlayer,
                                    IPacketSendService packetSendService)
        {
            _dialogFactory = dialogFactory;
            _searchResultsDialogFactory = searchResultsDialogFactory;
            _activeDialogRepository = activeDialogRepository;
            _enfFileProvider = enfFileProvider;
            _chatRepository = chatRepository;
            _sfxPlayer = sfxPlayer;
            _packetSendService = packetSendService;
        }

        public void ShowNpcInfo(int npcId)
        {
            var enfFile = _enfFileProvider.ENFFile;
            if (npcId <= 0 || npcId >= enfFile.Length)
                return;

            var npc = enfFile[npcId];
            if (string.IsNullOrEmpty(npc.Name))
                return;

            // Close any existing NPC info dialog
            _activeDialogRepository.NpcInfoDialog.MatchSome(dlg =>
            {
                if (dlg is BaseEODialog baseDialog) baseDialog.Close();
                else if (dlg is CodeDrawnScrollingListDialog codeDrawn) codeDrawn.Close();
            });

            var dlg = _dialogFactory.Create(npc);
            dlg.DialogClosed += (_, _) => _activeDialogRepository.NpcInfoDialog = Option.None<XNAControls.IXNADialog>();
            _activeDialogRepository.NpcInfoDialog = Option.Some(dlg);

            dlg.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            dlg.Show();

            // Request NPC source info from server
            _packetSendService.SendPacketAsync(new NpcSourceRequestPacket { NpcId = npcId });
        }

        public void ShowNoNpcsFound(string searchTerm)
        {
            var message = $"No NPCs found matching '{searchTerm}'";
            var chatData = new ChatData(ChatTab.Local, "System", message, ChatIcon.LookingDude);
            _chatRepository.AllChat[ChatTab.Local].Add(chatData);
        }

        public void ShowNpcSearchResults(int[] matchingNpcIds)
        {
            var enfFile = _enfFileProvider.ENFFile;

            // Close any existing search results dialog
            _activeDialogRepository.SearchResultsDialog.MatchSome(dlg => dlg.Close());

            // Create a code-drawn search results dialog
            var dlg = _searchResultsDialogFactory.Create($"Found {matchingNpcIds.Length} NPCs");

            // Add clickable items to the list
            foreach (var id in matchingNpcIds)
            {
                var npcRecord = enfFile[id];
                var capturedId = id;
                dlg.AddItem($"[{id}] {npcRecord.Name}", () => ShowNpcInfo(capturedId));
            }

            dlg.DialogClosed += (_, _) => _activeDialogRepository.SearchResultsDialog = Option.None<CodeDrawnSearchResultsDialog>();
            _activeDialogRepository.SearchResultsDialog = Option.Some(dlg);

            dlg.DialogClosing += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            dlg.Show();
        }
    }
}

