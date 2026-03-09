using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Dialogs.Services;
using EndlessClient.HUD.Inventory;
using EndlessClient.HUD.Spells;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Chat;
using EndlessClient.Services;
using EndlessClient.UI.Myra;
using EndlessClient.UI.Styles;
using Microsoft.Xna.Framework;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Chat;
using EOLib.Domain.Item;
using EOLib.Domain.Login;
using EOLib.Domain.Online;
using EOLib.Domain.Party;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;

namespace EndlessClient.HUD.Panels
{
    [MappedType(BaseType = typeof(IHudPanelFactory))]
    public class HudPanelFactory : IHudPanelFactory
    {
        private const int HUD_CONTROL_LAYER = 130;

        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IInventoryController _inventoryController;
        private readonly IChatActions _chatActions;
        private readonly IContentProvider _contentProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly INewsProvider _newsProvider;
        private readonly IChatProvider _chatProvider;
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly IPubFileProvider _pubFileProvider;
        private readonly IInventorySlotRepository _inventorySlotRepository;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ITrainingController _trainingController;
        private readonly IFriendIgnoreListService _friendIgnoreListService;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IItemStringService _itemStringService;
        private readonly IItemNameColorService _itemNameColorService;
        private readonly IInventoryService _inventoryService;
        private readonly IActiveDialogProvider _activeDialogProvider;
        private readonly ISpellSlotDataRepository _spellSlotDataRepository;
        private readonly IConfigurationRepository _configurationRepository;
        private readonly IOnlinePlayerProvider _onlinePlayerProvider;
        private readonly IOnlinePlayerActions _onlinePlayerActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IAudioActions _audioActions;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IPartyActions _partyActions;
        private readonly IPartyDataProvider _partyDataProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IUserInputProvider _userInputProvider;
        private readonly HUD.Macros.IMacroSlotDataRepository _macroSlotDataRepository;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IConfigFileSaveActions _configFileSaveActions;
        private readonly Game _game;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public HudPanelFactory(INativeGraphicsManager nativeGraphicsManager,
                               IInventoryController inventoryController,
                               IChatActions chatActions,
                               IContentProvider contentProvider,
                               IHudControlProvider hudControlProvider,
                               INewsProvider newsProvider,
                               IChatProvider chatProvider,
                               IPlayerInfoProvider playerInfoProvider,
                               ICharacterProvider characterProvider,
                               ICharacterInventoryProvider characterInventoryProvider,
                               IExperienceTableProvider experienceTableProvider,
                               IPubFileProvider pubFileProvider,
                               IInventorySlotRepository inventorySlotRepository,
                               IEOMessageBoxFactory messageBoxFactory,
                               ITrainingController trainingController,
                               IFriendIgnoreListService friendIgnoreListService,
                               IStatusLabelSetter statusLabelSetter,
                               IItemStringService itemStringService,
                               IItemNameColorService itemNameColorService,
                               IInventoryService inventoryService,
                               IActiveDialogProvider activeDialogProvider,
                               ISpellSlotDataRepository spellSlotDataRepository,
                               IConfigurationRepository configurationRepository,
                               IOnlinePlayerProvider onlinePlayerProvider,
                               IOnlinePlayerActions onlinePlayerActions,
                               ILocalizedStringFinder localizedStringFinder,
                               IAudioActions audioActions,
                               ISfxPlayer sfxPlayer,
                               IPartyActions partyActions,
                               IPartyDataProvider partyDataProvider,
                               IConfigurationProvider configurationProvider,
                               IClientWindowSizeProvider clientWindowSizeProvider,
                               IUserInputProvider userInputProvider,
                               HUD.Macros.IMacroSlotDataRepository macroSlotDataRepository,
                               IEODialogButtonService dialogButtonService,
                               IUIStyleProvider styleProvider,
                               IGraphicsDeviceProvider graphicsDeviceProvider,
                               IConfigFileSaveActions configFileSaveActions,
                               Game game,
                               IMyraUIManager myraUIManager,
                               IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _inventoryController = inventoryController;
            _chatActions = chatActions;
            _contentProvider = contentProvider;
            _hudControlProvider = hudControlProvider;
            _newsProvider = newsProvider;
            _chatProvider = chatProvider;
            _playerInfoProvider = playerInfoProvider;
            _characterProvider = characterProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _experienceTableProvider = experienceTableProvider;
            _pubFileProvider = pubFileProvider;
            _inventorySlotRepository = inventorySlotRepository;
            _messageBoxFactory = messageBoxFactory;
            _trainingController = trainingController;
            _friendIgnoreListService = friendIgnoreListService;
            _statusLabelSetter = statusLabelSetter;
            _itemStringService = itemStringService;
            _itemNameColorService = itemNameColorService;
            _inventoryService = inventoryService;
            _activeDialogProvider = activeDialogProvider;
            _spellSlotDataRepository = spellSlotDataRepository;
            _configurationRepository = configurationRepository;
            _onlinePlayerProvider = onlinePlayerProvider;
            _onlinePlayerActions = onlinePlayerActions;
            _localizedStringFinder = localizedStringFinder;
            _audioActions = audioActions;
            _sfxPlayer = sfxPlayer;
            _partyActions = partyActions;
            _partyDataProvider = partyDataProvider;
            _configurationProvider = configurationProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _userInputProvider = userInputProvider;
            _macroSlotDataRepository = macroSlotDataRepository;
            _dialogButtonService = dialogButtonService;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _configFileSaveActions = configFileSaveActions;
            _game = game;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IHudPanel CreateNewsPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraNewsPanel(_game, _myraUIManager, _myraFontProvider, _newsProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                var chatFont = _contentProvider.Fonts[Constants.FontSize11];
                return new NewsPanel(_nativeGraphicsManager,
                                     new ChatRenderableGenerator(_nativeGraphicsManager, _styleProvider, _friendIgnoreListService, chatFont),
                                     _newsProvider,
                                     chatFont,
                                     _clientWindowSizeProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public InventoryPanel CreateInventoryPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnInventoryPanel(_nativeGraphicsManager,
                    _inventoryController,
                    _statusLabelSetter,
                    _itemStringService,
                    _itemNameColorService,
                    _inventoryService,
                    _inventorySlotRepository,
                    _playerInfoProvider,
                    _characterProvider,
                    _characterInventoryProvider,
                    _pubFileProvider,
                    _hudControlProvider,
                    _activeDialogProvider,
                    _sfxPlayer,
                    _configurationProvider,
                    _styleProvider,
                    _graphicsDeviceProvider,
                    _contentProvider,
                    _clientWindowSizeProvider,
                    _userInputProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new InventoryPanel(_nativeGraphicsManager,
                    _inventoryController,
                    _statusLabelSetter,
                    _itemStringService,
                    _itemNameColorService,
                    _inventoryService,
                    _inventorySlotRepository,
                    _playerInfoProvider,
                    _characterProvider,
                    _characterInventoryProvider,
                    _pubFileProvider,
                    _hudControlProvider,
                    _activeDialogProvider,
                    _sfxPlayer,
                    _configurationProvider,
                    _clientWindowSizeProvider,
                    _userInputProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public ActiveSpellsPanel CreateActiveSpellsPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnActiveSpellsPanel(_nativeGraphicsManager,
                    _trainingController,
                    _messageBoxFactory,
                    _statusLabelSetter,
                    _playerInfoProvider,
                    _characterProvider,
                    _characterInventoryProvider,
                    _pubFileProvider,
                    _spellSlotDataRepository,
                    _hudControlProvider,
                    _sfxPlayer,
                    _configurationProvider,
                    _clientWindowSizeProvider,
                    _userInputProvider,
                    _styleProvider,
                    _graphicsDeviceProvider,
                    _contentProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new ActiveSpellsPanel(_nativeGraphicsManager,
                    _trainingController,
                    _messageBoxFactory,
                    _statusLabelSetter,
                    _playerInfoProvider,
                    _characterProvider,
                    _characterInventoryProvider,
                    _pubFileProvider,
                    _spellSlotDataRepository,
                    _hudControlProvider,
                    _sfxPlayer,
                    _configurationProvider,
                    _clientWindowSizeProvider,
                    _userInputProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreatePassiveSpellsPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraPassiveSpellsPanel(_game, _myraUIManager, _myraFontProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new PassiveSpellsPanel(_nativeGraphicsManager, _clientWindowSizeProvider) { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreateChatPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraChatPanel(_game, _myraUIManager, _myraFontProvider,
                    _chatActions, _chatProvider, _styleProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                var chatFont = _contentProvider.Fonts[Constants.FontSize11];
                return new ChatPanel(_nativeGraphicsManager,
                                     _chatActions,
                                     new ChatRenderableGenerator(_nativeGraphicsManager, _styleProvider, _friendIgnoreListService, chatFont),
                                     _chatProvider,
                                     _hudControlProvider,
                                     chatFont,
                                     _clientWindowSizeProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreateStatsPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraStatsPanel(_game, _myraUIManager, _myraFontProvider,
                    _characterProvider, _characterInventoryProvider,
                    _experienceTableProvider, _messageBoxFactory, _trainingController)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new StatsPanel(_nativeGraphicsManager,
                                      _characterProvider,
                                      _characterInventoryProvider,
                                      _experienceTableProvider,
                                      _messageBoxFactory,
                                      _trainingController,
                                      _clientWindowSizeProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreateOnlineListPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraOnlineListPanel(_game, _myraUIManager, _myraFontProvider,
                    _onlinePlayerProvider, _friendIgnoreListService, _partyDataProvider,
                    _characterProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                var chatFont = _contentProvider.Fonts[Constants.FontSize11];
                return new OnlineListPanel(_nativeGraphicsManager, _hudControlProvider, _onlinePlayerProvider, _partyDataProvider, _friendIgnoreListService, _sfxPlayer, chatFont, _clientWindowSizeProvider) { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreatePartyPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraPartyPanel(_game, _myraUIManager, _myraFontProvider,
                    _partyActions, _partyDataProvider, _characterProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new PartyPanel(_nativeGraphicsManager, _partyActions, _contentProvider, _partyDataProvider, _characterProvider, _clientWindowSizeProvider) { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreateSettingsPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraSettingsPanel(_game, _myraUIManager, _myraFontProvider,
                    _chatActions, _audioActions, _localizedStringFinder,
                    _messageBoxFactory, _configurationRepository, _sfxPlayer,
                    _configFileSaveActions)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new SettingsPanel(_nativeGraphicsManager,
                    _chatActions,
                    _audioActions,
                    _statusLabelSetter,
                    _localizedStringFinder,
                    _messageBoxFactory,
                    _configurationRepository,
                    _sfxPlayer,
                    _clientWindowSizeProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public MacroPanel CreateMacroPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new CodeDrawnMacroPanel(_nativeGraphicsManager,
                    _statusLabelSetter,
                    _playerInfoProvider,
                    _characterProvider,
                    _pubFileProvider,
                    _pubFileProvider,
                    _macroSlotDataRepository,
                    _sfxPlayer,
                    _configurationProvider,
                    _clientWindowSizeProvider,
                    _userInputProvider,
                    _dialogButtonService,
                    _styleProvider,
                    _graphicsDeviceProvider,
                    _contentProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new MacroPanel(_nativeGraphicsManager,
                    _statusLabelSetter,
                    _playerInfoProvider,
                    _characterProvider,
                    _pubFileProvider,
                    _pubFileProvider,
                    _macroSlotDataRepository,
                    _sfxPlayer,
                    _configurationProvider,
                    _clientWindowSizeProvider,
                    _userInputProvider,
                    _dialogButtonService)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
        }

        public IHudPanel CreateHelpPanel()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return new MyraHelpPanel(_game, _myraUIManager, _myraFontProvider)
                { DrawOrder = HUD_CONTROL_LAYER };
            }
            else
            {
                return new HelpPanel(_nativeGraphicsManager, _clientWindowSizeProvider) { DrawOrder = HUD_CONTROL_LAYER };
            }
        }
    }
}
