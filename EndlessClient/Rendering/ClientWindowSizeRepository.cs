using System;
using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib;
using EOLib.Graphics;
using XNAControls;

namespace EndlessClient.Rendering
{
    public interface IClientWindowSizeProvider
    {
        int Width { get; }
        int Height { get; }

        bool Resizable { get; }

        /// <summary>
        /// Returns the logical game width (640 pre-login, configured in-game).
        /// </summary>
        int GameWidth { get; }

        /// <summary>
        /// Returns the logical game height (480 pre-login, configured in-game).
        /// </summary>
        int GameHeight { get; }

        /// <summary>
        /// Current scale factor (window size / game size)
        /// </summary>
        float ScaleFactor { get; }

        /// <summary>
        /// Offset for letterboxing/pillarboxing when maintaining aspect ratio
        /// </summary>
        (int X, int Y) RenderOffset { get; }

        event EventHandler<EventArgs> GameWindowSizeChanged;
    }

    public interface IClientWindowSizeRepository : IResettable, IClientWindowSizeProvider
    {
        new int Width { get; set; }
        new int Height { get; set; }

        new bool Resizable { get; set; }

        /// <summary>
        /// Configured game width from settings (0 = use default 640)
        /// </summary>
        int ConfiguredGameWidth { get; set; }

        /// <summary>
        /// Configured game height from settings (0 = use default 480)
        /// </summary>
        int ConfiguredGameHeight { get; set; }

        /// <summary>
        /// Set to true when player is logged in (enables floating windows at configured dimensions)
        /// </summary>
        bool IsInGame { get; set; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class ClientWindowSizeRepository : IClientWindowSizeProvider, IClientWindowSizeRepository, IResettable, IGameViewportProvider
    {
        public const int DEFAULT_BACKBUFFER_WIDTH = 640;
        public const int DEFAULT_BACKBUFFER_HEIGHT = 480;

        private readonly IGameWindowRepository _gameWindowRepository;
        private readonly IGraphicsDeviceRepository _graphicsDeviceRepository;

        private readonly List<EventHandler<EventArgs>> _resizeEvents;

        // Actual window dimensions (use for resize logic and render offset calculations)
        private int WindowWidth => _gameWindowRepository.Window.ClientBounds.Width;
        private int WindowHeight => _gameWindowRepository.Window.ClientBounds.Height;

        public int Width
        {
            get => GameWidth;
            set
            {
                if (value < DEFAULT_BACKBUFFER_WIDTH)
                    value = DEFAULT_BACKBUFFER_WIDTH;

                _graphicsDeviceRepository.GraphicsDeviceManager.PreferredBackBufferWidth = value;
                _graphicsDeviceRepository.GraphicsDeviceManager.ApplyChanges();

                foreach (var evnt in _resizeEvents)
                    evnt(this, EventArgs.Empty);
            }
        }

        public int Height
        {
            get => GameHeight;
            set
            {
                if (value < DEFAULT_BACKBUFFER_HEIGHT)
                    value = DEFAULT_BACKBUFFER_HEIGHT;

                _graphicsDeviceRepository.GraphicsDeviceManager.PreferredBackBufferHeight = value;
                _graphicsDeviceRepository.GraphicsDeviceManager.ApplyChanges();

                foreach (var evnt in _resizeEvents)
                    evnt(this, EventArgs.Empty);
            }
        }

        public bool Resizable
        {
            // Pre-login: false (fixed 640x480 layout)
            // In-game with configured dimensions: true (floating layout)
            get => IsInGame && (ConfiguredGameWidth > DEFAULT_BACKBUFFER_WIDTH || ConfiguredGameHeight > DEFAULT_BACKBUFFER_HEIGHT);
            set => _gameWindowRepository.Window.AllowUserResizing = value;
        }

        public int ConfiguredGameWidth { get; set; }

        public int ConfiguredGameHeight { get; set; }

        public bool IsInGame { get; set; }

        // The base game resolution used for rendering:
        // - Pre-login: always 640x480 (fixed layout)
        // - In-game: configured dimensions (floating layout) or 640x480 if not configured
        private int BaseGameWidth => IsInGame && ConfiguredGameWidth > 0 ? ConfiguredGameWidth : DEFAULT_BACKBUFFER_WIDTH;
        private int BaseGameHeight => IsInGame && ConfiguredGameHeight > 0 ? ConfiguredGameHeight : DEFAULT_BACKBUFFER_HEIGHT;

        public int GameWidth => BaseGameWidth;

        public int GameHeight => BaseGameHeight;

        public float ScaleFactor
        {
            get
            {
                // Calculate the scale factor that fits the game in the window while maintaining aspect ratio
                float scaleX = (float)WindowWidth / BaseGameWidth;
                float scaleY = (float)WindowHeight / BaseGameHeight;
                return Math.Min(scaleX, scaleY);
            }
        }

        public (int X, int Y) RenderOffset
        {
            get
            {
                // Calculate letterbox/pillarbox offset for centered rendering
                int scaledWidth = (int)(BaseGameWidth * ScaleFactor);
                int scaledHeight = (int)(BaseGameHeight * ScaleFactor);

                int offsetX = (WindowWidth - scaledWidth) / 2;
                int offsetY = (WindowHeight - scaledHeight) / 2;

                return (offsetX, offsetY);
            }
        }

        public event EventHandler<EventArgs> GameWindowSizeChanged
        {
            add
            {
                _gameWindowRepository.Window.ClientSizeChanged += value;
                _resizeEvents.Add(value);
            }
            remove
            {
                _gameWindowRepository.Window.ClientSizeChanged -= value;
                _resizeEvents.Remove(value);
            }
        }

        public ClientWindowSizeRepository(IGameWindowRepository gameWindowRepository,
                                          IGraphicsDeviceRepository graphicsDeviceRepository)
        {
            _gameWindowRepository = gameWindowRepository;
            _graphicsDeviceRepository = graphicsDeviceRepository;
            _resizeEvents = new List<EventHandler<EventArgs>>();
        }

        public void ResetState()
        {
            foreach (var evnt in _resizeEvents)
                GameWindowSizeChanged -= evnt;
            _resizeEvents.Clear();

            IsInGame = false;
            // Window stays at its current size — render target handles scaling
        }
    }
}
