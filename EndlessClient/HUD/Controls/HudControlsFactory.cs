using System;
using System.Collections.Generic;
using EOLib.IO.Repositories;
using System.Linq;
using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Chat;
using EndlessClient.HUD.Panels;
using EndlessClient.HUD.Spells;
using EndlessClient.HUD.Toast;
using EndlessClient.HUD.StatusBars;
using EndlessClient.Input;
using EndlessClient.Network;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Character;
using EndlessClient.Rendering.Factories;
using EndlessClient.Rendering.Map;
using EndlessClient.Rendering.Metadata;
using EndlessClient.Rendering.Metadata.Models;
using EndlessClient.Rendering.NPC;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Chat;
using EOLib.Domain.Interact.Quest;
using EOLib.Domain.Login;
using EOLib.Domain.Map;
using EOLib.Domain.Pathing;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using EndlessClient.HUD.Windows;
using Microsoft.Xna.Framework;
using XNAControls;

namespace EndlessClient.HUD.Controls
{
    [AutoMappedType(IsSingleton = true)]
    public class HudControlsFactory : IHudControlsFactory
    {
        private const int HUD_BASE_LAYER = 100;
        private const int HUD_CONTROL_LAYER = 130;

        private readonly IHudButtonController _hudButtonController;
        private readonly IHudPanelFactory _hudPanelFactory;
        private readonly IMapRendererFactory _mapRendererFactory;
        private readonly IUserInputHandlerFactory _userInputHandlerFactory;
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IClientWindowSizeRepository _clientWindowSizeRepository;
        private readonly IEndlessGameProvider _endlessGameProvider;
        private readonly ICharacterRepository _characterRepository;
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly ICurrentMapStateRepository _currentMapStateRepository;
        private readonly IUserInputRepository _userInputRepository;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IStatusLabelTextProvider _statusLabelTextProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly IChatModeCalculator _chatModeCalculator;
        private readonly IExperienceTableProvider _experienceTableProvider;
        private readonly IPathFinder _pathFinder;
        private readonly ICharacterActions _characterActions;
        private readonly IWalkValidationActions _walkValidationActions;
        private readonly IChatBubbleActions _chatBubbleActions;
        private readonly IUnknownEntitiesRequestActions _unknownEntitiesRequestActions;
        private readonly IUserInputTimeProvider _userInputTimeProvider;
        private readonly ISpellSlotDataRepository _spellSlotDataRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IMiniMapRendererFactory _miniMapRendererFactory;
        private readonly INewsProvider _newsProvider;
        private readonly IFixedTimeStepRepository _fixedTimeStepRepository;
        private readonly IClickDispatcherFactory _clickDispatcherFactory;
        private readonly IMetadataProvider<WeaponMetadata> _weaponMetadataProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly ICharacterRendererProvider _characterRendererProvider;
        private readonly INPCRendererProvider _npcRendererProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly ICharacterSessionProvider _characterSessionProvider;
        private readonly ICharacterSessionRepository _characterSessionRepository;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IQuestActions _questActions;
        private readonly IBountyDataProvider _bountyDataProvider;
        private readonly IWindowZOrderManager _windowZOrderManager;
        private readonly IChatActions _chatActions;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ITextMultiInputDialogFactory _textMultiInputDialogFactory;
        private readonly ILockerDataRepository _lockerDataRepository;
        private readonly IBossHealthBarProvider _bossHealthBarProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private IChatController _chatController;
        private IMainButtonController _mainButtonController;

        public HudControlsFactory(IHudButtonController hudButtonController,
                                  IHudPanelFactory hudPanelFactory,
                                  IMapRendererFactory mapRendererFactory,
                                  IUserInputHandlerFactory userInputHandlerFactory,
                                  INativeGraphicsManager nativeGraphicsManager,
                                  IGraphicsDeviceProvider graphicsDeviceProvider,
                                  IClientWindowSizeRepository clientWindowSizeRepository,
                                  IEndlessGameProvider endlessGameProvider,
                                  ICharacterRepository characterRepository,
                                  IPlayerInfoProvider playerInfoProvider,
                                  ICurrentMapStateRepository currentMapStateRepository,
                                  IUserInputRepository userInputRepository,
                                  IStatusLabelSetter statusLabelSetter,
                                  IStatusLabelTextProvider statusLabelTextProvider,
                                  IContentProvider contentProvider,
                                  IHudControlProvider hudControlProvider,
                                  ICurrentMapProvider currentMapProvider,
                                  IChatModeCalculator chatModeCalculator,
                                  IExperienceTableProvider experienceTableProvider,
                                  IPathFinder pathFinder,
                                  ICharacterActions characterActions,
                                  IWalkValidationActions walkValidationActions,
                                  IChatBubbleActions chatBubbleActions,
                                  IUnknownEntitiesRequestActions unknownEntitiesRequestActions,
                                  IUserInputTimeProvider userInputTimeProvider,
                                  ISpellSlotDataRepository spellSlotDataRepository,
                                  ISfxPlayer sfxPlayer,
                                  IMiniMapRendererFactory miniMapRendererFactory,
                                  INewsProvider newsProvider,
                                  IFixedTimeStepRepository fixedTimeStepRepository,
                                  IClickDispatcherFactory clickDispatcherFactory,
                                  IMetadataProvider<WeaponMetadata> weaponMetadataProvider,
                                  ILocalizedStringFinder localizedStringFinder,
                                  ICharacterRendererProvider characterRendererProvider,
                                  INPCRendererProvider npcRendererProvider,
                                  IConfigurationProvider configurationProvider,
                                  IUIStyleProvider styleProvider,
                                  ICharacterSessionProvider characterSessionProvider,
                                  IQuestDataProvider questDataProvider,
                                  IQuestActions questActions,
                                  IBountyDataProvider bountyDataProvider,
                                  IWindowZOrderManager windowZOrderManager,
                                  IChatActions chatActions,
                                  ITextInputDialogFactory textInputDialogFactory,
                                  ITextMultiInputDialogFactory textMultiInputDialogFactory,
                                  ILockerDataRepository lockerDataRepository,
                                  IBossHealthBarProvider bossHealthBarProvider,
                                  IENFFileProvider enfFileProvider,
                                  ICharacterSessionRepository characterSessionRepository,
                                  ICharacterInventoryProvider characterInventoryProvider)
        {
            _hudButtonController = hudButtonController;
            _hudPanelFactory = hudPanelFactory;
            _mapRendererFactory = mapRendererFactory;
            _userInputHandlerFactory = userInputHandlerFactory;
            _nativeGraphicsManager = nativeGraphicsManager;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _clientWindowSizeRepository = clientWindowSizeRepository;
            _endlessGameProvider = endlessGameProvider;
            _characterRepository = characterRepository;
            _playerInfoProvider = playerInfoProvider;
            _currentMapStateRepository = currentMapStateRepository;
            _userInputRepository = userInputRepository;
            _statusLabelSetter = statusLabelSetter;
            _statusLabelTextProvider = statusLabelTextProvider;
            _contentProvider = contentProvider;
            _hudControlProvider = hudControlProvider;
            _currentMapProvider = currentMapProvider;
            _chatModeCalculator = chatModeCalculator;
            _experienceTableProvider = experienceTableProvider;
            _pathFinder = pathFinder;
            _characterActions = characterActions;
            _walkValidationActions = walkValidationActions;
            _chatBubbleActions = chatBubbleActions;
            _unknownEntitiesRequestActions = unknownEntitiesRequestActions;
            _userInputTimeProvider = userInputTimeProvider;
            _spellSlotDataRepository = spellSlotDataRepository;
            _sfxPlayer = sfxPlayer;
            _miniMapRendererFactory = miniMapRendererFactory;
            _newsProvider = newsProvider;
            _fixedTimeStepRepository = fixedTimeStepRepository;
            _clickDispatcherFactory = clickDispatcherFactory;
            _weaponMetadataProvider = weaponMetadataProvider;
            _localizedStringFinder = localizedStringFinder;
            _characterRendererProvider = characterRendererProvider;
            _npcRendererProvider = npcRendererProvider;
            _configurationProvider = configurationProvider;
            _styleProvider = styleProvider;
            _characterSessionProvider = characterSessionProvider;
            _questDataProvider = questDataProvider;
            _questActions = questActions;
            _bountyDataProvider = bountyDataProvider;
            _windowZOrderManager = windowZOrderManager;
            _chatActions = chatActions;
            _textInputDialogFactory = textInputDialogFactory;
            _textMultiInputDialogFactory = textMultiInputDialogFactory;
            _lockerDataRepository = lockerDataRepository;
            _bossHealthBarProvider = bossHealthBarProvider;
            _enfFileProvider = enfFileProvider;
            _characterSessionRepository = characterSessionRepository;
            _characterInventoryProvider = characterInventoryProvider;
        }

        public void InjectChatController(IChatController chatController,
                                         IMainButtonController mainButtonController)
        {
            _chatController = chatController;
            _mainButtonController = mainButtonController;
        }

        public IReadOnlyDictionary<HudControlIdentifier, IGameComponent> CreateHud()
        {
            var characterAnimator = CreateCharacterAnimator();
            var mapRenderer = _mapRendererFactory.CreateMapRenderer();

            var controls = new Dictionary<HudControlIdentifier, IGameComponent>
            {
                {HudControlIdentifier.CurrentUserInputTracker, CreateCurrentUserInputTracker()},

                {HudControlIdentifier.CharacterAnimator, characterAnimator},
                {HudControlIdentifier.NPCAnimator, CreateNPCAnimator()},
                {HudControlIdentifier.MapRenderer, mapRenderer},
                {HudControlIdentifier.StatusIcons, CreatePlayerStatusIconRenderer()},
                {HudControlIdentifier.MiniMapRenderer, _miniMapRendererFactory.Create()},

                {HudControlIdentifier.ClickDispatcher, CreateClickDispatcher(mapRenderer)},

                {HudControlIdentifier.HudBackground, CreateHudBackground()},

                {HudControlIdentifier.LeftButtonGroupBackground, CreateButtonGroupBackground(UI.Controls.CodeDrawnButtonGroupBackground.Side.Left)},
                {HudControlIdentifier.RightButtonGroupBackground, CreateButtonGroupBackground(UI.Controls.CodeDrawnButtonGroupBackground.Side.Right)},

                {HudControlIdentifier.InventoryButton, CreateStateChangeButton(InGameStates.Inventory)},
                {HudControlIdentifier.ViewMapButton, CreateStateChangeButton(InGameStates.ViewMapToggle)},
                {HudControlIdentifier.ActiveSpellsButton, CreateStateChangeButton(InGameStates.ActiveSpells)},
                {HudControlIdentifier.PassiveSpellsButton, CreateStateChangeButton(InGameStates.PassiveSpells)},
                {HudControlIdentifier.ChatButton, CreateStateChangeButton(InGameStates.Chat)},
                {HudControlIdentifier.StatsButton, CreateStateChangeButton(InGameStates.Stats)},
                {HudControlIdentifier.PaperdollButton, CreateStateChangeButton(InGameStates.Paperdoll)},
                {HudControlIdentifier.OnlineListButton, CreateStateChangeButton(InGameStates.OnlineList)},
                {HudControlIdentifier.PartyButton, CreateStateChangeButton(InGameStates.Party)},
                {HudControlIdentifier.MacroButton, CreateStateChangeButton(InGameStates.Macro)},
                {HudControlIdentifier.SettingsButton, CreateStateChangeButton(InGameStates.Settings)},

                {HudControlIdentifier.FriendList, CreateFriendListButton()},
                {HudControlIdentifier.IgnoreList, CreateIgnoreListButton()},

                {HudControlIdentifier.NewsPanel, CreateStatePanel(InGameStates.News)},
                {HudControlIdentifier.InventoryPanel, CreateStatePanel(InGameStates.Inventory)},
                {HudControlIdentifier.ActiveSpellsPanel, CreateStatePanel(InGameStates.ActiveSpells)},
                {HudControlIdentifier.PassiveSpellsPanel, CreateStatePanel(InGameStates.PassiveSpells)},
                {HudControlIdentifier.ChatPanel, CreateStatePanel(InGameStates.Chat)},
                {HudControlIdentifier.StatsPanel, CreateStatePanel(InGameStates.Stats)},
                {HudControlIdentifier.OnlineListPanel, CreateStatePanel(InGameStates.OnlineList)},
                {HudControlIdentifier.PartyPanel, CreateStatePanel(InGameStates.Party)},
                {HudControlIdentifier.MacroPanel, CreateStatePanel(InGameStates.Macro)},
                {HudControlIdentifier.SettingsPanel, CreateStatePanel(InGameStates.Settings)},
                {HudControlIdentifier.HelpPanel, CreateStatePanel(InGameStates.Help)},

                {HudControlIdentifier.ExpTrackerButton, CreateExpTrackerButton()},
                {HudControlIdentifier.QuestWindowButton, CreateQuestWindowButton()},
                {HudControlIdentifier.ExpTrackerWindow, CreateExpTrackerWindow()},
                {HudControlIdentifier.QuestTrackerWindow, CreateQuestTrackerWindow()},
                {HudControlIdentifier.QuestWindow, CreateQuestWindow()},
                {HudControlIdentifier.BountyTrackerButton, CreateBountyTrackerButton()},
                {HudControlIdentifier.BountyTrackerWindow, CreateBountyTrackerWindow()},
                {HudControlIdentifier.GuildInfoButton, CreateGuildInfoButton()},
                {HudControlIdentifier.GuildInfoWindow, CreateGuildInfoWindow()},
                {HudControlIdentifier.GuildPanelButton, CreateGuildPanelButton()},
                {HudControlIdentifier.GuildPanel, CreateGuildPanel()},

                {HudControlIdentifier.HPStatusBar, CreateHPStatusBar()},
                {HudControlIdentifier.TPStatusBar, CreateTPStatusBar()},
                {HudControlIdentifier.SPStatusBar, CreateSPStatusBar()},
                {HudControlIdentifier.TNLStatusBar, CreateTNLStatusBar()},
                {HudControlIdentifier.BossHealthBarHUD, CreateBossHealthBarHUD()},

                {HudControlIdentifier.ChatModePictureBox, CreateChatModePictureBox()},
                {HudControlIdentifier.ChatTextBox, CreateChatTextBox()},
                {HudControlIdentifier.ClockLabel, CreateClockLabel()},
                {HudControlIdentifier.StatusLabel, CreateStatusLabel()},
                {HudControlIdentifier.ToastManager, CreateToastManager()},

                {HudControlIdentifier.PeriodicStatUpdater, CreatePeriodicStatUpdater()},
                {HudControlIdentifier.UserInputHandler, CreateUserInputHandler()},
                {HudControlIdentifier.UnknownEntitiesRequester, CreateUnknownEntitiesRequester()},
                {HudControlIdentifier.PeriodicEmoteHandler, CreatePeriodicEmoteHandler(characterAnimator)},

                {HudControlIdentifier.PreviousUserInputTracker, CreatePreviousUserInputTracker()}
            };

            return controls;
        }

        private PlayerStatusIconRenderer CreatePlayerStatusIconRenderer()
        {
            return new PlayerStatusIconRenderer(
                _nativeGraphicsManager,
                (ICharacterProvider)_characterRepository,
                (ISpellSlotDataProvider)_spellSlotDataRepository,
                _currentMapProvider, _clientWindowSizeRepository);
        }

        private IClickDispatcher CreateClickDispatcher(IMapRenderer mapRenderer)
        {
            var dispatcher = _clickDispatcherFactory.Create();
            dispatcher.DrawOrder = mapRenderer.DrawOrder;
            return dispatcher;
        }

        private HudBackgroundFrame CreateHudBackground()
        {
            return new HudBackgroundFrame(_nativeGraphicsManager, _graphicsDeviceProvider)
            {
                DrawOrder = HUD_BASE_LAYER,
                Visible = !_clientWindowSizeRepository.Resizable,
            };
        }

        private IGameComponent CreateButtonGroupBackground(UI.Controls.CodeDrawnButtonGroupBackground.Side side)
        {
            return new UI.Controls.CodeDrawnButtonGroupBackground(_styleProvider, _clientWindowSizeRepository, side)
            {
                DrawOrder = HUD_CONTROL_LAYER,
                Visible = _configurationProvider.UIMode == UIMode.Code
            };
        }

        private IGameComponent CreateStateChangeButton(InGameStates whichState)
        {
            if (whichState == InGameStates.News)
                throw new ArgumentOutOfRangeException(nameof(whichState), "News state does not have a button associated with it");
            var buttonIndex = (int)whichState;

            // Code UI mode: use procedurally-drawn buttons with text labels
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                return CreateCodeDrawnStateChangeButton(whichState, buttonIndex);
            }

            var mainButtonTexture = _nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 25);
            var widthDelta = mainButtonTexture.Width / 2;
            var heightDelta = mainButtonTexture.Height / 11;

            IXNAButton retButton;
            if (!_clientWindowSizeRepository.Resizable)
            {
                var xPosition = buttonIndex < 6 ? 62 : 590;
                var yPosition = (buttonIndex < 6 ? 330 : 350) + (buttonIndex < 6 ? buttonIndex : buttonIndex - 6) * 20;

                retButton = new XNAButton(
                    mainButtonTexture,
                    new Vector2(xPosition, yPosition),
                    new Rectangle(0, heightDelta * buttonIndex, widthDelta, heightDelta),
                    new Rectangle(widthDelta, heightDelta * buttonIndex, widthDelta, heightDelta))
                {
                    DrawOrder = HUD_CONTROL_LAYER
                };
            }
            else
            {
                var yIndex = buttonIndex % 6 - 3;

                var xPosition = buttonIndex < 6 ? 0 : _clientWindowSizeRepository.Width - widthDelta;
                var yPosition = (_clientWindowSizeRepository.Height / 2 + heightDelta * yIndex);

                retButton = new XNAButton(
                    mainButtonTexture,
                    new Vector2(xPosition, yPosition),
                    new Rectangle(0, heightDelta * buttonIndex, widthDelta, heightDelta),
                    new Rectangle(widthDelta, heightDelta * buttonIndex, widthDelta, heightDelta))
                {
                    DrawOrder = HUD_CONTROL_LAYER
                };

                _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
                {
                    var capturedXPos = buttonIndex < 6 ? 0 : _clientWindowSizeRepository.Width - widthDelta;
                    var capturedYPos = (_clientWindowSizeRepository.Height / 2 + heightDelta * yIndex);
                    retButton.DrawPosition = new Vector2(capturedXPos, capturedYPos);
                };
            }

            retButton.OnMouseDown += (_, _) => DoHudStateChangeClick(whichState);
            retButton.OnMouseEnter += (_, _) => _statusLabelSetter.SetStatusLabel(
                EOResourceID.STATUS_LABEL_TYPE_BUTTON,
                EOResourceID.STATUS_LABEL_HUD_BUTTON_HOVER_FIRST + buttonIndex);

            return retButton;
        }

        // Right-side navigation buttons order (excluding Chat and Help which have no icon button)
        private static readonly InGameStates[] RightStackOrder = new[]
        {
            InGameStates.ViewMapToggle,
            InGameStates.Inventory,
            InGameStates.ActiveSpells,
            InGameStates.PassiveSpells,
            InGameStates.Stats,
            InGameStates.Paperdoll,
            InGameStates.Macro,
            InGameStates.OnlineList,
            InGameStates.Party,
            InGameStates.Settings,
        };

        private const int ICON_BUTTON_SIZE = 32;
        private const int ICON_BUTTON_GAP = 2;

        private int GetRightStackIndex(InGameStates state)
        {
            for (int i = 0; i < RightStackOrder.Length; i++)
                if (RightStackOrder[i] == state) return i;
            return -1;
        }

        private Point GetRightStackPosition(int stackIndex)
        {
            const int RowsPerColumn = 5;
            var col = stackIndex / RowsPerColumn; // 0 = left column, 1 = right column
            var row = stackIndex % RowsPerColumn;
            var totalHeight = RowsPerColumn * ICON_BUTTON_SIZE + (RowsPerColumn - 1) * ICON_BUTTON_GAP;
            var xPos = _clientWindowSizeRepository.Width - (2 - col) * (ICON_BUTTON_SIZE + ICON_BUTTON_GAP);
            var yStart = (_clientWindowSizeRepository.Height - totalHeight) / 2;
            var yPos = yStart + row * (ICON_BUTTON_SIZE + ICON_BUTTON_GAP);
            return new Point(xPos, yPos);
        }

        private Point GetLeftStackPosition(int stackIndex)
        {
            const int LeftStackCount = 5;
            var totalHeight = LeftStackCount * ICON_BUTTON_SIZE + (LeftStackCount - 1) * ICON_BUTTON_GAP;
            var yStart = (_clientWindowSizeRepository.Height - totalHeight) / 2;
            var yPos = yStart + stackIndex * (ICON_BUTTON_SIZE + ICON_BUTTON_GAP);
            return new Point(0, yPos);
        }

        private string GetIconTextureKey(InGameStates state)
        {
            return state switch
            {
                InGameStates.ViewMapToggle => ContentProvider.IconMap,
                InGameStates.Inventory => ContentProvider.IconInventory,
                InGameStates.ActiveSpells => ContentProvider.IconSpells,
                InGameStates.PassiveSpells => ContentProvider.IconPassive,
                InGameStates.Stats => ContentProvider.IconStats,
                InGameStates.Paperdoll => ContentProvider.IconEquip,
                InGameStates.Macro => ContentProvider.IconMacro,
                InGameStates.OnlineList => ContentProvider.IconOnline,
                InGameStates.Party => ContentProvider.IconParty,
                InGameStates.Settings => ContentProvider.IconConfig,
                _ => ContentProvider.IconConfig
            };
        }

        private string GetTooltipText(InGameStates state)
        {
            return state switch
            {
                InGameStates.ViewMapToggle => "Map",
                InGameStates.Inventory => "Inventory",
                InGameStates.ActiveSpells => "Spells",
                InGameStates.PassiveSpells => "Passive",
                InGameStates.Stats => "Stats",
                InGameStates.Paperdoll => "Equip",
                InGameStates.Macro => "Macro",
                InGameStates.OnlineList => "Online",
                InGameStates.Party => "Party",
                InGameStates.Settings => "Config",
                _ => state.ToString()
            };
        }

        private IGameComponent CreateCodeDrawnStateChangeButton(InGameStates whichState, int buttonIndex)
        {
            var stackIndex = GetRightStackIndex(whichState);

            // States not in the icon stack (Chat, Help) — hide them
            if (stackIndex < 0)
            {
                var placeholder = new XNALabel(Constants.FontSize08pt5) { DrawOrder = HUD_CONTROL_LAYER, Visible = false };
                return placeholder;
            }

            var iconKey = GetIconTextureKey(whichState);
            var pos = GetRightStackPosition(stackIndex);

            var btn = new UI.Controls.CodeDrawnIconButton(
                _styleProvider,
                _contentProvider.Textures[iconKey],
                _contentProvider.Fonts[EOLib.Shared.Constants.FontSize08pt5],
                _clientWindowSizeRepository)
            {
                TooltipText = GetTooltipText(whichState),
                TooltipOnLeft = true,
                DrawArea = new Rectangle(pos.X, pos.Y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE),
                DrawOrder = HUD_CONTROL_LAYER
            };

            btn.OnClick += (_, _) => DoHudStateChangeClick(whichState);
            btn.OnClick += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                var newPos = GetRightStackPosition(stackIndex);
                btn.DrawPosition = new Vector2(newPos.X, newPos.Y);
            };

            return btn;
        }

        private IXNAButton CreateFriendListButton()
        {
            Func<Vector2> getFriendListDrawPosition = () => new Vector2(_clientWindowSizeRepository.Width - 48, _clientWindowSizeRepository.Height - 37);
            var button = new XNAButton(
                _nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 27, false),
                _clientWindowSizeRepository.Resizable ? getFriendListDrawPosition() : new Vector2(592, 312),
                new Rectangle(0, 260, 17, 15),
                new Rectangle(0, 276, 17, 15))
            {
                DrawOrder = HUD_CONTROL_LAYER + 10
            };
            button.OnMouseDown += (_, _) => _hudButtonController.ClickFriendList();
            button.OnMouseDown += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
            button.OnMouseOver += (o, e) => _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_BUTTON, EOResourceID.STATUS_LABEL_FRIEND_LIST);

            if (_clientWindowSizeRepository.Resizable)
            {
                _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) => button.DrawPosition = getFriendListDrawPosition();
            }

            return button;
        }

        private IXNAButton CreateIgnoreListButton()
        {
            Func<Vector2> getIgnoreListDrawPosition = () => new Vector2(_clientWindowSizeRepository.Width - 31, _clientWindowSizeRepository.Height - 37);
            var button = new XNAButton(
                _nativeGraphicsManager.TextureFromResource(GFXTypes.PostLoginUI, 27, false),
                _clientWindowSizeRepository.Resizable ? getIgnoreListDrawPosition() : new Vector2(609, 312),
                new Rectangle(17, 260, 17, 15),
                new Rectangle(17, 276, 17, 15))
            {
                DrawOrder = HUD_CONTROL_LAYER + 10
            };
            button.OnMouseDown += (_, _) => _hudButtonController.ClickIgnoreList();
            button.OnMouseDown += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
            button.OnMouseOver += (o, e) => _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_BUTTON, EOResourceID.STATUS_LABEL_IGNORE_LIST);

            if (_clientWindowSizeRepository.Resizable)
            {
                _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) => button.DrawPosition = getIgnoreListDrawPosition();
            }

            return button;
        }

        private void DoHudStateChangeClick(InGameStates whichState)
        {
            switch (whichState)
            {
                case InGameStates.Inventory: _hudButtonController.ClickInventory(); break;
                case InGameStates.ViewMapToggle: _hudButtonController.ClickViewMapToggle(); break;
                case InGameStates.ActiveSpells: _hudButtonController.ClickActiveSpells(); break;
                case InGameStates.PassiveSpells: _hudButtonController.ClickPassiveSpells(); break;
                case InGameStates.Chat:
                    _hudButtonController.ClickChat();
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION, EOResourceID.STATUS_LABEL_CHAT_PANEL_NOW_VIEWED);
                    break;
                case InGameStates.Paperdoll:
                    _hudButtonController.ClickPaperdoll();
                    break;
                case InGameStates.Stats:
                    _hudButtonController.ClickStats();
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION, EOResourceID.STATUS_LABEL_STATS_PANEL_NOW_VIEWED);
                    break;
                case InGameStates.OnlineList:
                    _hudButtonController.ClickOnlineList();
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION, EOResourceID.STATUS_LABEL_ONLINE_PLAYERS_NOW_VIEWED);
                    break;
                case InGameStates.Party: _hudButtonController.ClickParty(); break;
                case InGameStates.Macro:
                    _hudButtonController.ClickMacro();
                    break;
                case InGameStates.Settings: _hudButtonController.ClickSettings(); break;
                case InGameStates.Help:
                    _hudButtonController.ClickHelp();
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION, EOResourceID.STATUS_LABEL_HUD_BUTTON_HOVER_LAST);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(whichState), whichState, null);
            }

            _sfxPlayer.PlaySfx(SoundEffectID.ButtonClick);
        }

        private IGameComponent CreateStatePanel(InGameStates whichState)
        {
            DraggableHudPanel retPanel;

            switch (whichState)
            {
                case InGameStates.Inventory: retPanel = _hudPanelFactory.CreateInventoryPanel(); break;
                case InGameStates.ActiveSpells: retPanel = _hudPanelFactory.CreateActiveSpellsPanel(); break;
                case InGameStates.PassiveSpells: retPanel = _hudPanelFactory.CreatePassiveSpellsPanel(); break;
                case InGameStates.Chat: retPanel = _hudPanelFactory.CreateChatPanel(); break;
                case InGameStates.Stats: retPanel = _hudPanelFactory.CreateStatsPanel(); break;
                case InGameStates.OnlineList: retPanel = _hudPanelFactory.CreateOnlineListPanel(); break;
                case InGameStates.Party: retPanel = _hudPanelFactory.CreatePartyPanel(); break;
                case InGameStates.Macro: retPanel = _hudPanelFactory.CreateMacroPanel(); break;
                case InGameStates.Settings: retPanel = _hudPanelFactory.CreateSettingsPanel(); break;
                case InGameStates.Help: retPanel = _hudPanelFactory.CreateHelpPanel(); break;
                case InGameStates.News: retPanel = _hudPanelFactory.CreateNewsPanel(); break;
                default: throw new ArgumentOutOfRangeException(nameof(whichState), whichState, "Panel specification is out of range.");
            }

            retPanel.Activated += () => retPanel.DrawOrder = _hudControlProvider.HudPanels.Select(x => x.DrawOrder).Max() + 1;

            // Register panels with WindowZOrderManager for dynamic PostScaleDrawOrder
            if (retPanel is IZOrderedWindow zOrderedPanel)
            {
                _windowZOrderManager.Register(zOrderedPanel);
                retPanel.Activated += () => _windowZOrderManager.BringToFront(zOrderedPanel);
            }

            // For Code UI mode with integrated chat panel, chat is always visible
            // Otherwise, show news if available, or chat if no news
            if (_configurationProvider.UIMode == UIMode.Code && whichState == InGameStates.Chat)
            {
                retPanel.Visible = true;

                // Wire up integrated chat panel events
                if (retPanel is CodeDrawnChatPanel codeDrawnChatPanel)
                {
                    codeDrawnChatPanel.OnEnterPressed += (_, _) => _chatController.SendChatAndClearTextBox();
                    codeDrawnChatPanel.OnInputClicked += (_, _) => _chatController.SelectChatTextBox();
                    codeDrawnChatPanel.OnInputTextChanged += (_, _) => _chatController.ClearAndWarnIfJailAndGlobal();
                }
            }
            else
            {
                retPanel.Visible = (_newsProvider.NewsText.Any() && whichState == InGameStates.News) ||
                                   (!_newsProvider.NewsText.Any() && whichState == InGameStates.Chat);
            }

            if (_clientWindowSizeRepository.Resizable)
            {
                retPanel.UpdateOrder = -1;

                UpdatePanelDrawPosition(initialPosition: true);
                _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) => UpdatePanelDrawPosition(initialPosition: false);

                var panelConfig = new IniReader(Constants.PanelLayoutFile);
                if (panelConfig.Load())
                {
                    if (panelConfig.GetValue("PANELS", $"{retPanel.GetType().Name}:X", out int x) &&
                        panelConfig.GetValue("PANELS", $"{retPanel.GetType().Name}:Y", out int y))
                    {
                        retPanel.DrawPosition = new Vector2(x, y);
                    }

                    // Don't let config override chat panel visibility in code UI mode
                    // (chat panel with integrated input should always be visible)
                    if (panelConfig.GetValue("PANELS", $"{retPanel.GetType().Name}:Visible", out bool visible) &&
                        !(_configurationProvider.UIMode == UIMode.Code && retPanel is CodeDrawnChatPanel))
                    {
                        retPanel.Visible = visible;
                    }

                    if (panelConfig.GetValue("PANELS", $"{retPanel.GetType().Name}:DrawOrder", out int drawOrder))
                    {
                        retPanel.DrawOrder = drawOrder;
                    }
                }
            }

            return retPanel;

            void UpdatePanelDrawPosition(bool initialPosition)
            {
                if (initialPosition)
                {
                    if (retPanel is CodeDrawnChatPanel)
                    {
                        // Chat panel anchors near the bottom
                        retPanel.DrawArea = retPanel.DrawArea.WithPosition(new Vector2(
                            (_clientWindowSizeRepository.Width - retPanel.DrawArea.Width) / 2,
                            _clientWindowSizeRepository.Height - 45 - retPanel.DrawArea.Height));
                    }
                    else
                    {
                        // Other panels center vertically in the window
                        retPanel.DrawArea = retPanel.DrawArea.WithPosition(new Vector2(
                            (_clientWindowSizeRepository.Width - retPanel.DrawArea.Width) / 2,
                            (_clientWindowSizeRepository.Height - retPanel.DrawArea.Height) / 2));
                    }
                }
                else
                {
                    if (_clientWindowSizeRepository.Width < retPanel.DrawPosition.X + retPanel.DrawArea.Width)
                        retPanel.DrawPosition = new Vector2(_clientWindowSizeRepository.Width - retPanel.DrawArea.Width, retPanel.DrawPosition.Y);

                    if (_clientWindowSizeRepository.Height < retPanel.DrawPosition.Y + retPanel.DrawArea.Height)
                        retPanel.DrawPosition = new Vector2(retPanel.DrawPosition.X, _clientWindowSizeRepository.Height - retPanel.DrawArea.Height);
                }
            }
        }


        private IGameComponent CreateExpTrackerButton()
        {
            var pos = GetLeftStackPosition(0);
            var btn = new UI.Controls.CodeDrawnIconButton(
                _styleProvider,
                _contentProvider.Textures[ContentProvider.IconExp],
                _contentProvider.Fonts[EOLib.Shared.Constants.FontSize08pt5],
                _clientWindowSizeRepository)
            {
                TooltipText = "Exp Tracker",
                DrawArea = new Rectangle(pos.X, pos.Y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE),
                DrawOrder = HUD_CONTROL_LAYER,
                Visible = _configurationProvider.UIMode == UIMode.Code
            };
            btn.OnClick += (_, _) => _hudButtonController.ClickExpTracker();
            btn.OnClick += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                var newPos = GetLeftStackPosition(0);
                btn.DrawPosition = new Vector2(newPos.X, newPos.Y);
            };

            return btn;
        }

        private IGameComponent CreateQuestWindowButton()
        {
            var pos = GetLeftStackPosition(1);
            var btn = new UI.Controls.CodeDrawnIconButton(
                _styleProvider,
                _contentProvider.Textures[ContentProvider.IconQuests],
                _contentProvider.Fonts[EOLib.Shared.Constants.FontSize08pt5],
                _clientWindowSizeRepository)
            {
                TooltipText = "Quests",
                DrawArea = new Rectangle(pos.X, pos.Y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE),
                DrawOrder = HUD_CONTROL_LAYER,
                Visible = _configurationProvider.UIMode == UIMode.Code
            };
            btn.OnClick += (_, _) => _hudButtonController.ClickQuestWindow();
            btn.OnClick += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                var newPos = GetLeftStackPosition(1);
                btn.DrawPosition = new Vector2(newPos.X, newPos.Y);
            };

            return btn;
        }

        private IGameComponent CreateExpTrackerWindow()
        {
            var window = new Windows.CodeDrawnExpTrackerWindow(
                (ICharacterProvider)_characterRepository,
                _characterSessionRepository,
                _characterSessionProvider,
                _experienceTableProvider,
                _styleProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _clientWindowSizeRepository,
                _characterInventoryProvider)
            {
                DrawOrder = HUD_CONTROL_LAYER + 20
            };

            _windowZOrderManager.Register(window);
            window.Activated += () => _windowZOrderManager.BringToFront(window);

            return window;
        }

        private IGameComponent CreateQuestTrackerWindow()
        {
            var window = new Windows.CodeDrawnQuestTrackerWindow(
                _styleProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _clientWindowSizeRepository,
                _questDataProvider,
                _questActions)
            {
                DrawOrder = HUD_CONTROL_LAYER + 25
            };

            _windowZOrderManager.Register(window);
            window.Activated += () => _windowZOrderManager.BringToFront(window);

            return window;
        }

        private IGameComponent CreateBountyTrackerButton()
        {
            var pos = GetLeftStackPosition(2);
            var btn = new UI.Controls.CodeDrawnIconButton(
                _styleProvider,
                _contentProvider.Textures[ContentProvider.IconBounties],
                _contentProvider.Fonts[EOLib.Shared.Constants.FontSize08pt5],
                _clientWindowSizeRepository)
            {
                TooltipText = "Bounties",
                DrawArea = new Rectangle(pos.X, pos.Y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE),
                DrawOrder = HUD_CONTROL_LAYER,
                Visible = _configurationProvider.UIMode == UIMode.Code
            };
            btn.OnClick += (_, _) => _hudButtonController.ClickBountyTracker();
            btn.OnClick += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                var newPos = GetLeftStackPosition(2);
                btn.DrawPosition = new Vector2(newPos.X, newPos.Y);
            };

            return btn;
        }

        private IGameComponent CreateBountyTrackerWindow()
        {
            var window = new Windows.CodeDrawnBountyTrackerWindow(
                _styleProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _clientWindowSizeRepository,
                _bountyDataProvider,
                _questActions)
            {
                DrawOrder = HUD_CONTROL_LAYER + 26
            };

            _windowZOrderManager.Register(window);
            window.Activated += () => _windowZOrderManager.BringToFront(window);

            return window;
        }

        private IGameComponent CreateGuildInfoButton()
        {
            var pos = GetLeftStackPosition(3);
            var btn = new UI.Controls.CodeDrawnIconButton(
                _styleProvider,
                _contentProvider.Textures[ContentProvider.IconGuildInfo],
                _contentProvider.Fonts[EOLib.Shared.Constants.FontSize08pt5],
                _clientWindowSizeRepository)
            {
                TooltipText = "Guild Info",
                DrawArea = new Rectangle(pos.X, pos.Y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE),
                DrawOrder = HUD_CONTROL_LAYER,
                Visible = _configurationProvider.UIMode == UIMode.Code
            };
            btn.OnClick += (_, _) => _hudButtonController.ClickGuildInfo();
            btn.OnClick += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                var newPos = GetLeftStackPosition(3);
                btn.DrawPosition = new Vector2(newPos.X, newPos.Y);
            };

            return btn;
        }

        private IGameComponent CreateGuildInfoWindow()
        {
            var window = new Windows.CodeDrawnGuildInfoWindow(
                _styleProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _clientWindowSizeRepository,
                _bountyDataProvider,
                _questActions)
            {
                DrawOrder = HUD_CONTROL_LAYER + 27
            };

            _windowZOrderManager.Register(window);
            window.Activated += () => _windowZOrderManager.BringToFront(window);

            return window;
        }

        private IGameComponent CreateGuildPanelButton()
        {
            var pos = GetLeftStackPosition(4);
            var btn = new UI.Controls.CodeDrawnIconButton(
                _styleProvider,
                _contentProvider.Textures[ContentProvider.IconGuildPanel],
                _contentProvider.Fonts[EOLib.Shared.Constants.FontSize08pt5],
                _clientWindowSizeRepository)
            {
                TooltipText = "Guild Panel",
                DrawArea = new Rectangle(pos.X, pos.Y, ICON_BUTTON_SIZE, ICON_BUTTON_SIZE),
                DrawOrder = HUD_CONTROL_LAYER,
                Visible = _configurationProvider.UIMode == UIMode.Code
            };
            btn.OnClick += (_, _) => _hudButtonController.ClickGuildPanel();
            btn.OnClick += (_, _) => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);

            _clientWindowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                var newPos = GetLeftStackPosition(4);
                btn.DrawPosition = new Vector2(newPos.X, newPos.Y);
            };

            return btn;
        }

        private IGameComponent CreateGuildPanel()
        {
            var window = new Windows.CodeDrawnGuildPanel(
                _styleProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _clientWindowSizeRepository,
                _bountyDataProvider,
                _questActions,
                _chatActions,
                _textInputDialogFactory,
                _textMultiInputDialogFactory,
                (ICharacterProvider)_characterRepository,
                _lockerDataRepository)
            {
                DrawOrder = HUD_CONTROL_LAYER + 28
            };

            _windowZOrderManager.Register(window);
            window.Activated += () => _windowZOrderManager.BringToFront(window);

            return window;
        }

        private IGameComponent CreateQuestWindow()
        {
            var questWindow = new Windows.CodeDrawnQuestWindow(
                (ICharacterProvider)_characterRepository,
                _questDataProvider,
                _questActions,
                _localizedStringFinder,
                _styleProvider,
                _graphicsDeviceProvider,
                _contentProvider,
                _clientWindowSizeRepository)
            {
                DrawOrder = HUD_CONTROL_LAYER + 20
            };

            _windowZOrderManager.Register(questWindow);
            questWindow.Activated += () => _windowZOrderManager.BringToFront(questWindow);

            // Link the quest window to the tracker window after controls are created
            // This will be done via Initialize or a separate linking step
            return questWindow;
        }

        private IGameComponent CreateHPStatusBar()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var statusBar = new CodeDrawnHPStatusBar(_clientWindowSizeRepository, (ICharacterProvider)_characterRepository, _styleProvider, _graphicsDeviceProvider, _contentProvider) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
            else
            {
                var statusBar = new HPStatusBar(_nativeGraphicsManager, _clientWindowSizeRepository, (ICharacterProvider)_characterRepository) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
        }

        private IGameComponent CreateTPStatusBar()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var statusBar = new CodeDrawnTPStatusBar(_clientWindowSizeRepository, (ICharacterProvider)_characterRepository, _styleProvider, _graphicsDeviceProvider, _contentProvider) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
            else
            {
                var statusBar = new TPStatusBar(_nativeGraphicsManager, _clientWindowSizeRepository, (ICharacterProvider)_characterRepository) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
        }

        private IGameComponent CreateSPStatusBar()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var statusBar = new CodeDrawnSPStatusBar(_clientWindowSizeRepository, (ICharacterProvider)_characterRepository, _styleProvider, _graphicsDeviceProvider, _contentProvider) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
            else
            {
                var statusBar = new SPStatusBar(_nativeGraphicsManager, _clientWindowSizeRepository, (ICharacterProvider)_characterRepository) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
        }

        private IGameComponent CreateTNLStatusBar()
        {
            if (_configurationProvider.UIMode == UIMode.Code)
            {
                var statusBar = new CodeDrawnTNLStatusBar(_clientWindowSizeRepository, (ICharacterProvider)_characterRepository, _styleProvider, _graphicsDeviceProvider, _contentProvider, _experienceTableProvider) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
            else
            {
                var statusBar = new TNLStatusBar(_nativeGraphicsManager, _clientWindowSizeRepository, (ICharacterProvider)_characterRepository, _experienceTableProvider) { DrawOrder = HUD_CONTROL_LAYER };
                statusBar.StatusBarClicked += () => _sfxPlayer.PlaySfx(SoundEffectID.HudStatusBarClick);
                return statusBar;
            }
        }

        private ChatModePictureBox CreateChatModePictureBox()
        {
            return new ChatModePictureBox(_nativeGraphicsManager, _clientWindowSizeRepository, _chatModeCalculator, _hudControlProvider)
            {
                DrawOrder = HUD_CONTROL_LAYER + 1
            };
        }

        private ChatTextBox CreateChatTextBox()
        {
            var chatTextBox = new ChatTextBox(_nativeGraphicsManager, _clientWindowSizeRepository, _contentProvider, _configurationProvider)
            {
                Text = "",
                Selected = true,
                // Hide standalone ChatTextBox when using code UI mode (integrated in chat panel)
                Visible = _configurationProvider.UIMode != UIMode.Code,
                DrawOrder = HUD_CONTROL_LAYER,
            };
            chatTextBox.OnEnterPressed += (_, _) => _chatController.SendChatAndClearTextBox();
            chatTextBox.OnClicked += (_, _) => _chatController.SelectChatTextBox();
            chatTextBox.OnTextChanged += (_, _) => _chatController.ClearAndWarnIfJailAndGlobal();

            return chatTextBox;
        }

        private TimeLabel CreateClockLabel()
        {
            return new TimeLabel(_clientWindowSizeRepository) { DrawOrder = HUD_CONTROL_LAYER + 1 };
        }

        private PeriodicStatUpdaterComponent CreatePeriodicStatUpdater()
        {
            return new PeriodicStatUpdaterComponent(_endlessGameProvider, _characterRepository);
        }

        private UnknownEntitiesRequester CreateUnknownEntitiesRequester()
        {
            return new UnknownEntitiesRequester(_endlessGameProvider, _clientWindowSizeRepository, (ICharacterProvider)_characterRepository, _currentMapStateRepository,
                _npcRendererProvider, _characterRendererProvider, _unknownEntitiesRequestActions);
        }

        private StatusBarLabel CreateStatusLabel()
        {
            return new StatusBarLabel(_nativeGraphicsManager, _clientWindowSizeRepository, _statusLabelTextProvider)
            {
                // Hide in scaled mode - toast notifications replace this
                Visible = !_clientWindowSizeRepository.Resizable,
                DrawOrder = HUD_CONTROL_LAYER,
            };
        }

        private CodeDrawnToastManager CreateToastManager()
        {
            return new CodeDrawnToastManager(
                _endlessGameProvider,
                _clientWindowSizeRepository,
                _styleProvider,
                _contentProvider)
            {
                // Only visible in scaled mode
                Visible = _clientWindowSizeRepository.Resizable
            };
        }

        private CurrentUserInputTracker CreateCurrentUserInputTracker()
        {
            // Initialize the scroll wheel consumed helper for scrollbar-to-zoom coordination
            ScrollWheelConsumedHelper.Initialize(_userInputRepository);
            return new CurrentUserInputTracker(_endlessGameProvider, _userInputRepository, _clientWindowSizeRepository);
        }

        private IUserInputHandler CreateUserInputHandler()
        {
            return _userInputHandlerFactory.CreateUserInputHandler();
        }

        private ICharacterAnimator CreateCharacterAnimator()
        {
            return new CharacterAnimator(
                _endlessGameProvider, _characterRepository, _playerInfoProvider, _currentMapStateRepository,
                _currentMapProvider, _spellSlotDataRepository, _characterActions,
                _walkValidationActions, _pathFinder, _fixedTimeStepRepository,
                _weaponMetadataProvider);
        }

        private INPCAnimator CreateNPCAnimator()
        {
            return new NPCAnimator(_endlessGameProvider, _currentMapStateRepository, _fixedTimeStepRepository);
        }

        private IPeriodicEmoteHandler CreatePeriodicEmoteHandler(ICharacterAnimator characterAnimator)
        {
            return new PeriodicEmoteHandler(
                _endlessGameProvider, _characterActions, _chatBubbleActions,
                _userInputTimeProvider, _characterRepository, characterAnimator,
                _statusLabelSetter, _mainButtonController, _localizedStringFinder,
                _sfxPlayer);
        }

        private PreviousUserInputTracker CreatePreviousUserInputTracker()
        {
            return new PreviousUserInputTracker(_endlessGameProvider, _userInputRepository);
        }

        private BossHealthBarHUD CreateBossHealthBarHUD()
        {
            return new BossHealthBarHUD(
                _endlessGameProvider,
                _bossHealthBarProvider,
                _npcRendererProvider,
                _enfFileProvider,
                _clientWindowSizeRepository,
                _contentProvider)
            {
                DrawOrder = HUD_CONTROL_LAYER + 50
            };
        }
    }
}
