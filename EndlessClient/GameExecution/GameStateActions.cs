using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.ControlSets;
using EndlessClient.HUD.Panels;
using EndlessClient.Network;
using EndlessClient.Rendering;
using EOLib.Config;
using EOLib.Domain.Login;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using XNAControls.Input;

namespace EndlessClient.GameExecution
{
    [MappedType(BaseType = typeof(IGameStateActions))]
    public class GameStateActions : IGameStateActions
    {
        private readonly IGameStateRepository _gameStateRepository;
        private readonly IControlSetRepository _controlSetRepository;
        private readonly IControlSetFactory _controlSetFactory;
        private readonly IEndlessGameProvider _endlessGameProvider;
        private readonly IPlayerInfoRepository _playerInfoRepository;
        private readonly IClientWindowSizeRepository _clientWindowSizeRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IMfxPlayer _mfxPlayer;

        public GameStateActions(IGameStateRepository gameStateRepository,
                                IControlSetRepository controlSetRepository,
                                IControlSetFactory controlSetFactory,
                                IEndlessGameProvider endlessGameProvider,
                                IPlayerInfoRepository playerInfoRepository,
                                IClientWindowSizeRepository clientWindowSizeRepository,
                                ISfxPlayer sfxPlayer,
                                IMfxPlayer mfxPlayer)
        {
            _gameStateRepository = gameStateRepository;
            _controlSetRepository = controlSetRepository;
            _controlSetFactory = controlSetFactory;
            _endlessGameProvider = endlessGameProvider;
            _playerInfoRepository = playerInfoRepository;
            _clientWindowSizeRepository = clientWindowSizeRepository;
            _sfxPlayer = sfxPlayer;
            _mfxPlayer = mfxPlayer;
        }

        public void ChangeToState(GameStates newState)
        {
            if (newState == _gameStateRepository.CurrentState)
                return;

            if (_gameStateRepository.CurrentState == GameStates.PlayingTheGame)
            {
                _playerInfoRepository.PlayerIsInGame = false;
                _clientWindowSizeRepository.IsInGame = false;

                StorePanelLayout(Game, new ExitingEventArgs());
                Game.Exiting -= StorePanelLayout;
            }

            var currentSet = _controlSetRepository.CurrentControlSet;

            // For PlayingTheGame state, set IsInGame BEFORE creating controls
            // so they initialize with the correct Resizable state (floating layout)
            // Note: Window resize is deferred to after controls are added
            if (newState == GameStates.PlayingTheGame)
            {
                _clientWindowSizeRepository.IsInGame = true;
            }

            var nextSet = _controlSetFactory.CreateControlsForState(newState, currentSet);

            RemoveOldComponents(currentSet, nextSet);
            AddNewComponents(nextSet);

            switch (newState)
            {
                case GameStates.None:
                case GameStates.Initial:
                    {
                        if (newState == GameStates.None ||
                            _gameStateRepository.CurrentState == GameStates.PlayingTheGame)
                        {
                            _sfxPlayer.StopLoopingSfx();

                            // this replicates behavior of the vanilla client where returning to the main menu stops playing the background music
                            _mfxPlayer.StopBackgroundMusic();
                        }
                    }
                    break;
                case GameStates.LoggedIn: _sfxPlayer.PlaySfx(SoundEffectID.Login); break;
                case GameStates.PlayingTheGame:
                    {
                        // IsInGame and window resize already handled above before control set creation
                        Game.Exiting += StorePanelLayout;
                    }
                    break;
            }

            _gameStateRepository.CurrentState = newState;
            _controlSetRepository.CurrentControlSet = nextSet;

            // Deferred window resize for PlayingTheGame (after controls fully added)
            if (newState == GameStates.PlayingTheGame &&
                _clientWindowSizeRepository.IsScaledMode &&
                (_clientWindowSizeRepository.ConfiguredGameWidth > 0 || _clientWindowSizeRepository.ConfiguredGameHeight > 0))
            {
                var newWidth = _clientWindowSizeRepository.GameWidth;
                var newHeight = _clientWindowSizeRepository.GameHeight;
                System.Console.WriteLine($"[SCALED MODE] Switching to in-game: {newWidth}x{newHeight}");
                _clientWindowSizeRepository.Width = newWidth;
                _clientWindowSizeRepository.Height = newHeight;
            }
        }

        public void RefreshCurrentState()
        {
            var currentSet = _controlSetRepository.CurrentControlSet;
            var emptySet = new EmptyControlSet();

            RemoveOldComponents(currentSet, emptySet);
            var refreshedSet = _controlSetFactory.CreateControlsForState(currentSet.GameState, emptySet);
            AddNewComponents(refreshedSet);
            _controlSetRepository.CurrentControlSet = refreshedSet;
        }

        public void ExitGame()
        {
            Game.Exit();
        }

        private void AddNewComponents(IControlSet nextSet)
        {
            foreach (var component in nextSet.AllComponents.Except(nextSet.XNAControlComponents))
                if (!Game.Components.Contains(component))
                    Game.Components.Add(component);
        }

        private void RemoveOldComponents(IControlSet currentSet, IControlSet nextSet)
        {
            var componentsToRemove = FindUnusedComponents(currentSet, nextSet);
            var disposableComponents = componentsToRemove
                .Where(x => x is not PacketHandlerGameComponent && x is not InputManager && x is not DispatcherGameComponent)
                .OfType<IDisposable>();

            foreach (var component in disposableComponents)
                component.Dispose();
            foreach (var component in componentsToRemove.Where(Game.Components.Contains))
                Game.Components.Remove(component);

            currentSet.Dispose();
        }

        private List<IGameComponent> FindUnusedComponents(IControlSet current, IControlSet next)
        {
            return current.AllComponents
                .Where(component => !next.AllComponents.Contains(component))
                .ToList();
        }

        private void StorePanelLayout(object sender, ExitingEventArgs e)
        {
            if (!_clientWindowSizeRepository.Resizable) return;

            var panelConfig = new IniReader(Constants.PanelLayoutFile);
            panelConfig.Sections["PANELS"] = new SortedList<string, string>();

            var panels = _controlSetRepository.CurrentControlSet.AllComponents.OfType<DraggableHudPanel>();
            foreach (var panel in panels)
            {
                panelConfig.Sections["PANELS"][$"{panel.GetType().Name}:X"] = ((int)panel.DrawPositionWithParentOffset.X).ToString();
                panelConfig.Sections["PANELS"][$"{panel.GetType().Name}:Y"] = ((int)panel.DrawPositionWithParentOffset.Y).ToString();
                panelConfig.Sections["PANELS"][$"{panel.GetType().Name}:Visible"] = panel.Visible.ToString();
                panelConfig.Sections["PANELS"][$"{panel.GetType().Name}:DrawOrder"] = panel.DrawOrder.ToString();
            }

            panelConfig.Sections["DISPLAY"] = new SortedList<string, string>();
            panelConfig.Sections["DISPLAY"]["Width"] = _clientWindowSizeRepository.Width.ToString();
            panelConfig.Sections["DISPLAY"]["Height"] = _clientWindowSizeRepository.Height.ToString();

            panelConfig.Save();
        }

        private IEndlessGame Game => _endlessGameProvider.Game;
    }
}
