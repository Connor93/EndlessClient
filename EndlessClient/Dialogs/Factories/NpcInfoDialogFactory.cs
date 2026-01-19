using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EOLib.Domain.Interact;
using EOLib.Graphics;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class NpcInfoDialogFactory : INpcInfoDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly INpcSourceProvider _npcSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;

        public NpcInfoDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                    IEODialogButtonService dialogButtonService,
                                    INpcSourceProvider npcSourceProvider,
                                    IEIFFileProvider eifFileProvider,
                                    IENFFileProvider enfFileProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _npcSourceProvider = npcSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;
        }

        public NpcInfoDialog Create(ENFRecord npc)
        {
            // Load NPC graphic from GFXTypes.NPC
            // Formula: (graphic - 1) * 40 + frame_offset (1 = standing south)
            var npcGraphic = npc.Graphic > 0
                ? _nativeGraphicsManager.TextureFromResource(GFXTypes.NPC, (npc.Graphic - 1) * 40 + 1, transparent: true)
                : null;

            return new NpcInfoDialog(_nativeGraphicsManager, _dialogButtonService,
                                     _npcSourceProvider, _eifFileProvider, _enfFileProvider,
                                     npc, npcGraphic);
        }
    }

    public interface INpcInfoDialogFactory
    {
        NpcInfoDialog Create(ENFRecord npc);
    }
}
