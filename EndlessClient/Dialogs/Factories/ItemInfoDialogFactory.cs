using AutomaticTypeMapper;
using EndlessClient.Dialogs.Services;
using EOLib.Graphics;
using EOLib.IO.Pub;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class ItemInfoDialogFactory : IItemInfoDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;

        public ItemInfoDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                     IEODialogButtonService dialogButtonService)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
        }

        public ItemInfoDialog Create(EIFRecord item)
        {
            // Load item graphic from GFXTypes.Items (23)
            var itemGraphic = item.Graphic > 0
                ? _nativeGraphicsManager.TextureFromResource(GFXTypes.Items, 2 * item.Graphic - 1, transparent: true)
                : null;

            return new ItemInfoDialog(_nativeGraphicsManager, _dialogButtonService, item, itemGraphic);
        }
    }

    public interface IItemInfoDialogFactory
    {
        ItemInfoDialog Create(EIFRecord item);
    }
}
