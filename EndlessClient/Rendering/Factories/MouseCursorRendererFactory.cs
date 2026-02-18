using AutomaticTypeMapper;
using EndlessClient.Controllers;
using EndlessClient.Dialogs;
using EndlessClient.HUD;
using EndlessClient.Input;
using EOLib.Config;
using EOLib.Domain.Item;
using EOLib.Domain.Map;
using EOLib.Graphics;
using EOLib.IO.Repositories;

namespace EndlessClient.Rendering.Factories
{
    [AutoMappedType]
    public class MouseCursorRendererFactory : IMouseCursorRendererFactory
    {
        private readonly IGridDrawCoordinateCalculator _gridDrawCoordinateCalculator;
        private readonly IMapCellStateProvider _mapCellStateProvider;
        private readonly IItemStringService _itemStringService;
        private readonly IItemNameColorService _itemNameColorService;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly IUserInputProvider _userInputProvider;
        private readonly IActiveDialogProvider _activeDialogProvider;
        private readonly IContextMenuProvider _contextMenuProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly ICurrentMapStateProvider _currentMapStateProvider;

        public MouseCursorRendererFactory(IGridDrawCoordinateCalculator gridDrawCoordinateCalculator,
                                          IMapCellStateProvider mapCellStateProvider,
                                          IItemStringService itemStringService,
                                          IItemNameColorService itemNameColorService,
                                          IEIFFileProvider eifFileProvider,
                                          ICurrentMapProvider currentMapProvider,
                                          IUserInputProvider userInputProvider,
                                          IActiveDialogProvider activeDialogProvider,
                                          IContextMenuProvider contextMenuProvider,
                                          IConfigurationProvider configurationProvider,
                                          IClientWindowSizeProvider clientWindowSizeProvider,
                                          IGraphicsDeviceProvider graphicsDeviceProvider,
                                          ICurrentMapStateProvider currentMapStateProvider)
        {
            _gridDrawCoordinateCalculator = gridDrawCoordinateCalculator;
            _mapCellStateProvider = mapCellStateProvider;
            _itemStringService = itemStringService;
            _itemNameColorService = itemNameColorService;
            _eifFileProvider = eifFileProvider;
            _currentMapProvider = currentMapProvider;
            _userInputProvider = userInputProvider;
            _activeDialogProvider = activeDialogProvider;
            _contextMenuProvider = contextMenuProvider;
            _configurationProvider = configurationProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _currentMapStateProvider = currentMapStateProvider;
        }

        public IMouseCursorRenderer Create()
        {
            return new MouseCursorRenderer(_gridDrawCoordinateCalculator,
                                           _mapCellStateProvider,
                                           _itemStringService,
                                           _itemNameColorService,
                                           _eifFileProvider,
                                           _currentMapProvider,
                                           _userInputProvider,
                                           _activeDialogProvider,
                                           _contextMenuProvider,
                                           _configurationProvider,
                                           _clientWindowSizeProvider,
                                           _graphicsDeviceProvider,
                                           _currentMapStateProvider);
        }
    }

    public interface IMouseCursorRendererFactory
    {
        IMouseCursorRenderer Create();
    }
}
