using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EOLib.Domain.Interact;
using EOLib.Graphics;
using EOLib.IO.Pub;
using EOLib.IO.Repositories;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class ItemInfoDialogFactory : IItemInfoDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IItemSourceProvider _itemSourceProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;

        public ItemInfoDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                     IEODialogButtonService dialogButtonService,
                                     IItemSourceProvider itemSourceProvider,
                                     IEIFFileProvider eifFileProvider,
                                     IENFFileProvider enfFileProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _itemSourceProvider = itemSourceProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;
        }

        public ItemInfoDialog Create(EIFRecord item)
        {
            // Load item graphic from GFXTypes.Items (23)
            var itemGraphic = item.Graphic > 0
                ? _nativeGraphicsManager.TextureFromResource(GFXTypes.Items, 2 * item.Graphic - 1, transparent: true)
                : null;

            return new ItemInfoDialog(_nativeGraphicsManager, _dialogButtonService,
                                       _itemSourceProvider, _eifFileProvider, _enfFileProvider,
                                       item, itemGraphic);
        }
    }

    public interface IItemInfoDialogFactory
    {
        ItemInfoDialog Create(EIFRecord item);
    }
}
