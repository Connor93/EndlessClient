using System.Collections.Generic;
using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Controls;
using EndlessClient.Network;
using EndlessClient.Rendering;
using EndlessClient.UIControls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;
using XNAControls.Input;

namespace EndlessClient.Initialization
{
    [AutoMappedType]
    public class EndlessClientInitializer : IGameInitializer
    {
        private readonly IEndlessGame _game;
        private readonly IEndlessGameRepository _endlessGameRepository;
        private readonly IContentProvider _contentProvider;
        private readonly List<IGameComponent> _persistentComponents;

        private readonly IMainButtonController _mainButtonController;
        private readonly IAccountController _accountController;
        private readonly ILoginController _loginController;
        private readonly ICharacterManagementController _characterManagementController;
        private readonly IChatController _chatController;
        private readonly IInventoryController _inventoryController;
        private readonly IControlSetFactory _controlSetFactory;
        private readonly ICharacterInfoPanelFactory _characterInfoPanelFactory;
        private readonly IHudControlsFactory _hudControlsFactory;
        private readonly IPaperdollDialogFactory _paperdollDialogFactory;
        private readonly DispatcherGameComponent _dispatcherGameComponent;
        private readonly PacketHandlerGameComponent _packetHandlerGameComponent;

        public EndlessClientInitializer(IEndlessGame game,
                                        IEndlessGameRepository endlessGameRepository,
                                        IContentProvider contentProvider,
                                        List<IGameComponent> persistentComponents,

                                        //Todo: refactor method injection to something like IEnumerable<IMethodInjectable>
                                        IMainButtonController mainButtonController,
                                        IAccountController accountController,
                                        ILoginController loginController,
                                        ICharacterManagementController characterManagementController,
                                        IChatController chatController,
                                        IInventoryController inventoryController,
                                        //factories
                                        IControlSetFactory controlSetFactory,
                                        ICharacterInfoPanelFactory characterInfoPanelFactory,
                                        IHudControlsFactory hudControlsFactory,
                                        IPaperdollDialogFactory paperdollDialogFactory,
                                        // Persistent game components (required for macOS manual registration)
                                        DispatcherGameComponent dispatcherGameComponent,
                                        PacketHandlerGameComponent packetHandlerGameComponent)
        {
            System.IO.File.AppendAllText("debug_log.txt", "EndlessClientInitializer Constructor START\n");
            _game = game;
            _endlessGameRepository = endlessGameRepository;
            _contentProvider = contentProvider;
            _persistentComponents = persistentComponents;
            _mainButtonController = mainButtonController;
            _accountController = accountController;
            _loginController = loginController;
            _characterManagementController = characterManagementController;
            _chatController = chatController;
            _inventoryController = inventoryController;
            _controlSetFactory = controlSetFactory;
            _characterInfoPanelFactory = characterInfoPanelFactory;
            _hudControlsFactory = hudControlsFactory;
            _paperdollDialogFactory = paperdollDialogFactory;
            _dispatcherGameComponent = dispatcherGameComponent;
            _packetHandlerGameComponent = packetHandlerGameComponent;
        }

        public void Initialize()
        {
            GameRepository.SetGame(_game as Game);

            foreach (var component in _persistentComponents)
                _game.Components.Add(component);

            // Add persistent game components (fixes macOS manual registration issue)
            _game.Components.Add(_dispatcherGameComponent);
            _game.Components.Add(_packetHandlerGameComponent);

            var mouseListenerSettings = new MouseListenerSettings
            {
                DoubleClickMilliseconds = 150,
                DragThreshold = 1
            };
            _game.Components.Add(new InputManager(GameRepository.GetGame(), mouseListenerSettings));

            _endlessGameRepository.Game = _game;

            // On macOS, MonoGame prepends Contents/Resources to content paths.
            // Use absolute path to bypass this when running outside app bundle.
            var contentDir = "ContentPipeline";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                var bundlePath = System.IO.Path.Combine("Contents", "Resources", "ContentPipeline");
                if (!System.IO.Directory.Exists(bundlePath))
                {
                    // Running in development mode - use absolute path
                    contentDir = System.IO.Path.GetFullPath("ContentPipeline");
                }
            }
            _game.Content.RootDirectory = contentDir;
            _contentProvider.SetContentManager(_game.Content);

            _controlSetFactory.InjectControllers(_mainButtonController,
                                                 _accountController,
                                                 _loginController,
                                                 _characterManagementController);
            _characterInfoPanelFactory.InjectCharacterManagementController(_characterManagementController);
            _characterInfoPanelFactory.InjectLoginController(_loginController);
            _hudControlsFactory.InjectChatController(_chatController, _mainButtonController);
            _paperdollDialogFactory.InjectInventoryController(_inventoryController);
        }
    }
}
