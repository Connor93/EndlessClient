using System;
using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib;
using EOLib.Graphics;

namespace EndlessClient.Rendering
{
    public interface IClientWindowSizeProvider
    {
        int Width { get; }
        int Height { get; }

        bool Resizable { get; }

        /// <summary>
        /// When in scaled mode, returns the logical game width (640). Otherwise same as Width.
        /// </summary>
        int GameWidth { get; }

        /// <summary>
        /// When in scaled mode, returns the logical game height (480). Otherwise same as Height.
        /// </summary>
        int GameHeight { get; }

        /// <summary>
        /// Whether the client is in scaled rendering mode
        /// </summary>
        bool IsScaledMode { get; }

        /// <summary>
        /// Current scale factor (1.0 when not scaled, otherwise window size / game size)
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

        new bool IsScaledMode { get; set; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class ClientWindowSizeRepository : IClientWindowSizeProvider, IClientWindowSizeRepository, IResettable
    {
        public const int DEFAULT_BACKBUFFER_WIDTH = 640;
        public const int DEFAULT_BACKBUFFER_HEIGHT = 480;

        private readonly IGameWindowRepository _gameWindowRepository;
        private readonly IGraphicsDeviceRepository _graphicsDeviceRepository;

        private readonly List<EventHandler<EventArgs>> _resizeEvents;

        public int Width
        {
            get => _gameWindowRepository.Window.ClientBounds.Width;
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
            get => _gameWindowRepository.Window.ClientBounds.Height;
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
            // In scaled mode, return false so UI uses fixed layout (even though window can resize)
            get => !IsScaledMode && _gameWindowRepository.Window.AllowUserResizing;
            set => _gameWindowRepository.Window.AllowUserResizing = value;
        }

        public bool IsScaledMode { get; set; }

        public int GameWidth => IsScaledMode ? DEFAULT_BACKBUFFER_WIDTH : Width;

        public int GameHeight => IsScaledMode ? DEFAULT_BACKBUFFER_HEIGHT : Height;

        public float ScaleFactor
        {
            get
            {
                if (!IsScaledMode) return 1.0f;

                // Calculate the scale factor that fits the game in the window while maintaining aspect ratio
                float scaleX = (float)Width / DEFAULT_BACKBUFFER_WIDTH;
                float scaleY = (float)Height / DEFAULT_BACKBUFFER_HEIGHT;
                return Math.Min(scaleX, scaleY);
            }
        }

        public (int X, int Y) RenderOffset
        {
            get
            {
                if (!IsScaledMode) return (0, 0);

                // Calculate letterbox/pillarbox offset for centered rendering
                int scaledWidth = (int)(DEFAULT_BACKBUFFER_WIDTH * ScaleFactor);
                int scaledHeight = (int)(DEFAULT_BACKBUFFER_HEIGHT * ScaleFactor);

                int offsetX = (Width - scaledWidth) / 2;
                int offsetY = (Height - scaledHeight) / 2;

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

            Resizable = false;

            Width = DEFAULT_BACKBUFFER_WIDTH;
            Height = DEFAULT_BACKBUFFER_HEIGHT;
        }
    }
}
