using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Input;
using EndlessClient.Rendering.Character;
using EOLib.Config;
using EOLib.Domain.Character;
using EOLib.Domain.Extensions;
using EOLib.Domain.Map;
using EOLib.IO.Map;
using Microsoft.Xna.Framework;

namespace EndlessClient.Rendering.Map
{
    [AutoMappedType(IsSingleton = true)]
    public class DynamicMapObjectUpdater : IDynamicMapObjectUpdater
    {
        private const int DOOR_CLOSE_TIME_MS = 3000;

        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterRendererProvider _characterRendererProvider;
        private readonly ICurrentMapStateRepository _currentMapStateRepository;
        private readonly ICurrentMapProvider _currentMapProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IUserInputProvider _userInputProvider;
        private readonly ISfxPlayer _sfxPlayer;

        private readonly List<(Warp Door, DateTime OpenTime)> _cachedDoorState;
        private IMapFile _cachedMap;
        private List<MapCoordinate> _ambientSounds;

        public DynamicMapObjectUpdater(ICharacterProvider characterProvider,
                                       ICharacterRendererProvider characterRendererProvider,
                                       ICurrentMapStateRepository currentMapStateRepository,
                                       ICurrentMapProvider currentMapProvider,
                                       IConfigurationProvider configurationProvider,
                                       IClientWindowSizeProvider clientWindowSizeProvider,
                                       IUserInputProvider userInputProvider,
                                       ISfxPlayer sfxPlayer)
        {
            _characterProvider = characterProvider;
            _characterRendererProvider = characterRendererProvider;
            _currentMapStateRepository = currentMapStateRepository;
            _currentMapProvider = currentMapProvider;
            _configurationProvider = configurationProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _userInputProvider = userInputProvider;
            _sfxPlayer = sfxPlayer;

            _cachedDoorState = new List<(Warp Door, DateTime OpenTime)>();
            _ambientSounds = new List<MapCoordinate>();
        }

        public void UpdateMapObjects(GameTime gameTime)
        {
            // todo: this should probably be part of currentMapStateRepository instead of tracked here
            if (_cachedMap != _currentMapProvider.CurrentMap)
            {
                _ambientSounds = new List<MapCoordinate>(_currentMapProvider.CurrentMap.GetTileSpecs(TileSpec.AmbientSource));
                _cachedMap = _currentMapProvider.CurrentMap;
            }

            var now = DateTime.Now;
            OpenNewDoors(now);
            CloseExpiredDoors(now);

            UpdateAmbientNoiseVolume();

            HideStackedCharacterNames();
        }

        private void OpenNewDoors(DateTime now)
        {
            var newDoors = _currentMapStateRepository.OpenDoors.Where(x => _cachedDoorState.All(d => d.Door != x));
            foreach (var door in newDoors)
            {
                _cachedDoorState.Add((door, now));
                _sfxPlayer.PlaySfx(SoundEffectID.DoorOpen);
            }
        }

        private void CloseExpiredDoors(DateTime now)
        {
            var expiredDoors = _cachedDoorState.Where(x => (now - x.OpenTime).TotalMilliseconds > DOOR_CLOSE_TIME_MS).ToList();
            foreach (var door in expiredDoors)
            {
                _cachedDoorState.Remove(door);
                if (_currentMapStateRepository.OpenDoors.Contains(door.Door))
                {
                    _currentMapStateRepository.OpenDoors.Remove(door.Door);
                    _sfxPlayer.PlaySfx(SoundEffectID.DoorClose);
                }
            }
        }

        private void UpdateAmbientNoiseVolume()
        {
            if (_cachedMap.Properties.AmbientNoise <= 0 || !_configurationProvider.SoundEnabled)
                return;

            // the algorithm in EO main seems to scale volume with distance to the closest ambient source
            // distance is the sum of the components of the vector from character position to closest ambient source
            // this is scaled from 0-25, with 0 being on top of the tile and 25 being too far away to hear the ambient sound from it
            var props = _characterProvider.MainCharacter.RenderProperties;
            var charCoord = props.CurrentAction == CharacterActionState.Walking ? props.DestinationCoordinates() : props.Coordinates();
            var shortestDistance = int.MaxValue;
            foreach (var coordinate in _ambientSounds)
            {
                var distance = Math.Abs(charCoord.X - coordinate.X) + Math.Abs(charCoord.Y - coordinate.Y);
                if (distance < shortestDistance)
                    shortestDistance = distance;
            }
            _sfxPlayer.SetLoopingSfxVolume(Math.Max((25 - shortestDistance) / 25f, 0));
        }

        private void HideStackedCharacterNames()
        {
            var mousePos = GetZoomAdjustedMousePosition();
            var characters = _characterRendererProvider.CharacterRenderers.Values
                .Where(x => x.DrawArea.Contains(mousePos))
                .GroupBy(x => x.Character.RenderProperties.Coordinates());

            foreach (var grouping in characters)
            {
                if (grouping.Count() > 1)
                {
                    var isFirst = true;
                    foreach (var character in grouping.Reverse())
                    {
                        if (isFirst)
                        {
                            character.ShowName();
                        }
                        else
                        {
                            character.HideName();
                        }

                        isFirst = false;
                    }
                }
                else
                {
                    foreach (var character in grouping)
                        character.ShowName();
                }
            }
        }

        private Point GetZoomAdjustedMousePosition()
        {
            var mousePos = _userInputProvider.CurrentMouseState.Position;

            // First: transform from window coords to game coords (scaled mode)
            if (_clientWindowSizeProvider.IsScaledMode)
            {
                var offset = _clientWindowSizeProvider.RenderOffset;
                var scale = _clientWindowSizeProvider.ScaleFactor;
                mousePos = new Point(
                    (int)((mousePos.X - offset.X) / scale),
                    (int)((mousePos.Y - offset.Y) / scale));
            }

            // Second: apply inverse zoom transform
            var zoom = _configurationProvider.MapZoom;
            if (zoom == 1.0f)
                return mousePos;

            var centerX = _clientWindowSizeProvider.GameWidth / 2f;
            var centerY = _clientWindowSizeProvider.GameHeight / 2f;
            return new Point(
                (int)((mousePos.X - centerX) / zoom + centerX),
                (int)((mousePos.Y - centerY) / zoom + centerY));
        }
    }

    public interface IDynamicMapObjectUpdater
    {
        void UpdateMapObjects(GameTime gameTime);
    }
}
