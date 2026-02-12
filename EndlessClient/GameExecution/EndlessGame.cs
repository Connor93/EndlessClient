using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Chat;
using EndlessClient.Test;
using EndlessClient.UIControls;
using EOLib;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.IO.Actions;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

#if DEBUG
using System.Diagnostics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input;
#endif

namespace EndlessClient.GameExecution
{
    [MappedType(BaseType = typeof(IEndlessGame), IsSingleton = true)]
    public class EndlessGame : Game, IEndlessGame
    {
        private readonly IClientWindowSizeRepository _windowSizeRepository;
        private readonly IContentProvider _contentProvider;
        private readonly IGraphicsDeviceRepository _graphicsDeviceRepository;
        private readonly IGameWindowRepository _gameWindowRepository;
        private readonly IControlSetRepository _controlSetRepository;
        private readonly IControlSetFactory _controlSetFactory;
        private readonly ITestModeLauncher _testModeLauncher;
        private readonly IPubFileLoadActions _pubFileLoadActions;
        private readonly IChatBubbleTextureProvider _chatBubbleTextureProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMfxPlayer _mfxPlayer;
        private readonly IXnaControlSoundMapper _soundMapper;
        private readonly IFixedTimeStepRepository _fixedTimeStepRepository;
        private readonly IMainButtonController _mainButtonController;
        private readonly IScrollingListDialogFactory _scrollingListDialogFactory;

        private GraphicsDeviceManager _graphicsDeviceManager;

        private KeyboardState _previousKeyState;
        private TimeSpan _lastFrameUpdate;
        private TimeSpan _lastDrawTime;
        private TimeSpan _minDrawInterval;

#if DEBUG
        private SpriteBatch _spriteBatch;
        private Stopwatch _lastFrameRenderTime = Stopwatch.StartNew();
        private int _frames, _displayFrames;
        private Texture2D _black;
#else
        private SpriteBatch _spriteBatch;
#endif

        // Render target scaling fields
        private RenderTarget2D _gameRenderTarget;

        public EndlessGame(IClientWindowSizeRepository windowSizeRepository,
                           IContentProvider contentProvider,
                           IGraphicsDeviceRepository graphicsDeviceRepository,
                           IGameWindowRepository gameWindowRepository,
                           IControlSetRepository controlSetRepository,
                           IControlSetFactory controlSetFactory,
                           ITestModeLauncher testModeLauncher,
                           IPubFileLoadActions pubFileLoadActions,
                           IChatBubbleTextureProvider chatBubbleTextureProvider,
                           IConfigurationProvider configurationProvider,
                           IMfxPlayer mfxPlayer,
                           IXnaControlSoundMapper soundMapper,
                           IFixedTimeStepRepository fixedTimeStepRepository,
                           IMainButtonController mainButtonController,
                           IScrollingListDialogFactory scrollingListDialogFactory)
        {

            _windowSizeRepository = windowSizeRepository;
            _contentProvider = contentProvider;
            _graphicsDeviceRepository = graphicsDeviceRepository;
            _gameWindowRepository = gameWindowRepository;
            _controlSetRepository = controlSetRepository;
            _controlSetFactory = controlSetFactory;
            _testModeLauncher = testModeLauncher;
            _pubFileLoadActions = pubFileLoadActions;
            _chatBubbleTextureProvider = chatBubbleTextureProvider;
            _configurationProvider = configurationProvider;
            _mfxPlayer = mfxPlayer;
            _soundMapper = soundMapper;
            _fixedTimeStepRepository = fixedTimeStepRepository;
            _mainButtonController = mainButtonController;
            _scrollingListDialogFactory = scrollingListDialogFactory;

            _graphicsDeviceManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH,
                PreferredBackBufferHeight = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT
            };
        }

        protected override void Initialize()
        {
            Components.ComponentAdded += (o, e) =>
            {
                // this is bad hack
                // all pre-game controls have a specific sound that should be mapped to them.
                // in-game controls get their sounds mapped individually.
                //
                // Checking for GameStates.LoggedIn because the in-game controls are
                //     added to the components in the LoggedIn state
                if (_controlSetRepository.CurrentControlSet.GameState != GameStates.LoggedIn)
                {
                    _soundMapper.BindSoundToControl(e.GameComponent);
                }
            };

            base.Initialize();

            IsMouseVisible = true;
            IsFixedTimeStep = false;

            TargetElapsedTime = TimeSpan.FromMilliseconds(FixedTimeStepRepository.TICK_TIME_MS);
            InactiveSleepTime = TimeSpan.FromMilliseconds(0);

            _previousKeyState = Keyboard.GetState();

            // Set up FPS limiter based on config (0 = unlimited)
            var maxFps = _configurationProvider.MaxFPS;
            _minDrawInterval = maxFps > 0
                ? TimeSpan.FromMilliseconds(1000.0 / maxFps)
                : TimeSpan.Zero;

            // setting Width/Height in window size repository applies the change to disable vsync
            _graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            _graphicsDeviceManager.IsFullScreen = false;

            // Set window to configured dimensions (or default 640x480)
            var windowWidth = _configurationProvider.InGameWidth > 0
                ? _configurationProvider.InGameWidth
                : ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH;
            var windowHeight = _configurationProvider.InGameHeight > 0
                ? _configurationProvider.InGameHeight
                : ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT;
            _graphicsDeviceManager.PreferredBackBufferWidth = windowWidth;
            _graphicsDeviceManager.PreferredBackBufferHeight = windowHeight;
            _graphicsDeviceManager.ApplyChanges();

            _windowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                if (_windowSizeRepository.Width < ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT)
                    _windowSizeRepository.Width = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH;

                if (_windowSizeRepository.Height < ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT)
                    _windowSizeRepository.Height = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT;

                // Recreate the game render target if dimensions changed
                if (_gameRenderTarget != null)
                {
                    var newWidth = _windowSizeRepository.GameWidth;
                    var newHeight = _windowSizeRepository.GameHeight;
                    if (_gameRenderTarget.Width != newWidth || _gameRenderTarget.Height != newHeight)
                    {
                        _gameRenderTarget.Dispose();
                        _gameRenderTarget = new RenderTarget2D(
                            GraphicsDevice,
                            newWidth,
                            newHeight,
                            false,
                            SurfaceFormat.Color,
                            DepthFormat.None);
                    }
                }
            };

            Exiting += (_, _) => _mfxPlayer.StopBackgroundMusic();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
#if DEBUG
            _black = new Texture2D(GraphicsDevice, 1, 1);
            _black.SetData(new[] { Color.Black });
#endif

            _contentProvider.Load();

            //todo: all the things that should load stuff as part of game's load/initialize should be broken into a pattern
            _chatBubbleTextureProvider.LoadContent();

            //the GraphicsDevice/Window don't exist until Initialize() is called by the framework
            //Ideally, this would be set in a DependencyContainer, but I'm not sure of a way to do that now
            _graphicsDeviceRepository.GraphicsDevice = GraphicsDevice;
            _graphicsDeviceRepository.GraphicsDeviceManager = _graphicsDeviceManager;
            _gameWindowRepository.Window = Window;

            // Set configured game dimensions for when player logs in (0 = use default 640x480)
            _windowSizeRepository.ConfiguredGameWidth = _configurationProvider.InGameWidth;
            _windowSizeRepository.ConfiguredGameHeight = _configurationProvider.InGameHeight;

            // Enable window resizing for scaling
            Window.AllowUserResizing = true;

            // Create render target at game resolution for scaled rendering
            _gameRenderTarget = new RenderTarget2D(
                GraphicsDevice,
                ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH,
                ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            SetUpInitialControlSet();

            if (_configurationProvider.MusicEnabled)
            {
                _mfxPlayer.PlayBackgroundMusic(1, EOLib.IO.Map.MusicControl.InterruptPlayRepeat);
            }

            AttemptToLoadPubFiles();

            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            // Fixed timestep catch-up loop: run multiple ticks if the PC fell behind.
            // On fast PCs this runs once per loop (same as before). On slow PCs, missed ticks
            // are caught up so animations/movement run at correct speed. Capped at 5 to prevent spiral-of-death.
            //
            // IMPORTANT: base.Update() is only called ONCE per frame to preserve input handling.
            // Calling it multiple times causes XNA input components to consume mouse clicks on
            // the first iteration, breaking NPC interaction and other click-based features.
            // Catch-up ticks only advance the tick counter for animation timing.
            var catchUpCount = 0;
            const int MaxCatchUpTicks = 5;
            while ((gameTime.TotalGameTime - _lastFrameUpdate).TotalMilliseconds >= FixedTimeStepRepository.TICK_TIME_MS
                   && catchUpCount < MaxCatchUpTicks)
            {
                _fixedTimeStepRepository.Tick();
                _lastFrameUpdate += TimeSpan.FromMilliseconds(FixedTimeStepRepository.TICK_TIME_MS);
                catchUpCount++;
            }

            // Run game component updates exactly once per frame
            if (catchUpCount > 0)
            {
#if DEBUG
                var currentKeyState = Keyboard.GetState();
                if (KeyboardExtended.GetState().WasKeyJustDown(Keys.F5))
                {
                    _testModeLauncher.LaunchTestMode();
                }

                _previousKeyState = currentKeyState;
#endif

                try
                {
                    base.Update(gameTime);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Collection was modified"))
                {
                    // XNAControls has a race condition where modifying controls during click event
                    // enumeration causes a crash. Log and continue - the operation will retry next frame.
                    System.Diagnostics.Debug.WriteLine($"[XNAControls] Collection modification during input: {ex.Message}");
                }
#if DEBUG
                catch
                {
                    throw;
                }
#else
                catch (Exception ex)
                {
                    _mainButtonController.GoToInitialStateAndDisconnect(showLostConnection: false);
                    ShowExceptionDetailDialog(ex);
                }
#endif
            }

            // FPS limiter: suppress next draw if not enough time has elapsed
            if (_minDrawInterval > TimeSpan.Zero)
            {
                var elapsed = gameTime.TotalGameTime - _lastDrawTime;
                if (elapsed < _minDrawInterval)
                {
                    SuppressDraw();
                }
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            _lastDrawTime = gameTime.TotalGameTime;

            var isTestMode = _controlSetRepository.CurrentControlSet.GameState == GameStates.TestMode;

            // Check if render target needs to be resized (e.g., after logout when game dimensions change)
            var targetWidth = _windowSizeRepository.GameWidth;
            var targetHeight = _windowSizeRepository.GameHeight;
            if (_gameRenderTarget.Width != targetWidth || _gameRenderTarget.Height != targetHeight)
            {
                _gameRenderTarget.Dispose();
                _gameRenderTarget = new RenderTarget2D(
                    GraphicsDevice,
                    targetWidth,
                    targetHeight,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None);
            }

            // Render the game to the fixed-size render target
            GraphicsDevice.SetRenderTarget(_gameRenderTarget);
            GraphicsDevice.Clear(isTestMode ? Color.White : Color.Black);

            base.Draw(gameTime);

            // Switch back to the main backbuffer and draw the scaled render target
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black); // Letterbox/pillarbox color

            // Calculate destination rectangle for scaled rendering
            var scale = _windowSizeRepository.ScaleFactor;
            var offset = _windowSizeRepository.RenderOffset;
            var destRect = new Rectangle(
                offset.X,
                offset.Y,
                (int)(_windowSizeRepository.GameWidth * scale),
                (int)(_windowSizeRepository.GameHeight * scale));

            // Draw scaled using point sampling for crisp pixels
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_gameRenderTarget, destRect, Color.White);
            _spriteBatch.End();

            // Draw post-scale UI controls directly to backbuffer for crisp rendering
            DrawPostScaleControls(scale, new Point(offset.X, offset.Y));

#if DEBUG
            _frames++;

            var fpsString = $"FPS: {_displayFrames}{(gameTime.IsRunningSlowly ? " (SLOW)" : string.Empty)}";
            var dim = _contentProvider.Fonts[Constants.FontSize09].MeasureString(fpsString);

            _spriteBatch.Begin();
            _spriteBatch.Draw(_black, new Rectangle(18, 18, (int)dim.Width + 4, (int)dim.Height + 4), Color.White);
            _spriteBatch.DrawString(_contentProvider.Fonts[Constants.FontSize09], fpsString, new Vector2(20, 20), Color.White);
            _spriteBatch.End();

            if (_lastFrameRenderTime.ElapsedMilliseconds > 1000)
            {
                _displayFrames = _frames;
                _frames = 0;
                _lastFrameRenderTime = Stopwatch.StartNew();
            }
#endif
        }

        private void AttemptToLoadPubFiles()
        {
            List<Action> pubFileLoadActions = [
                _pubFileLoadActions.LoadItemFile,
                _pubFileLoadActions.LoadNPCFile,
                _pubFileLoadActions.LoadSpellFile,
                _pubFileLoadActions.LoadClassFile
            ];

            foreach (var action in pubFileLoadActions)
            {
                try
                {
                    action();
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException)
                {
                }
            }
        }

        private void SetUpInitialControlSet()
        {
            var controls = _controlSetFactory.CreateControlsForState(
                GameStates.Initial,
                _controlSetRepository.CurrentControlSet);
            _controlSetRepository.CurrentControlSet = controls;

            //since the controls are being created in LoadContent(), adding them to the default game
            //  doesn't call the Initialize() method on any controls, so it must be done here
            foreach (var xnaControl in _controlSetRepository.CurrentControlSet.AllComponents)
                xnaControl.Initialize();
        }

        private void ShowExceptionDetailDialog(Exception ex)
        {
            var dlg = _scrollingListDialogFactory.Create(Dialogs.DialogType.Message);
            dlg.Title = "Unhandled Exception";
            dlg.Buttons = Dialogs.ScrollingListDialogButtons.Ok;
            dlg.AddTextAsListItems(
                _contentProvider.Fonts[Constants.FontSize08pt5],
                insertLineBreaks: true,
                linkClickActions: [() => GithubIssueGenerator.FileIssue(ex)],
                $"Client caused an exception",
                ex.ToString(),
                "*Report this exception as a GitHub issue");
            dlg.ShowDialog();
        }

        private void DrawPostScaleControls(float scaleFactor, Point renderOffset)
        {
            // Find all post-scale drawable controls and sort by draw order
            // Lower order values are drawn first (background), higher values are drawn later (foreground)
            var seen = new HashSet<IPostScaleDrawable>();
            var postScaleDrawables = new List<IPostScaleDrawable>();

            // Collect from Game.Components (non-IXNAControl game components)
            // Snapshot with ToList() to avoid collection-modified exceptions when dialogs close during draw
            foreach (var component in Components.ToList())
            {
                CollectPostScaleDrawables(component, postScaleDrawables, seen);
            }

            // Also collect from control set's AllComponents, which includes IXNAControl instances
            // that are excluded from Game.Components by GameStateActions.AddNewComponents
            foreach (var component in _controlSetRepository.CurrentControlSet.AllComponents.ToList())
            {
                CollectPostScaleDrawables(component, postScaleDrawables, seen);
            }

            // Sort by PostScaleDrawOrder - lower values first, dialogs (100) on top of HUD panels (0)
            postScaleDrawables.Sort((a, b) => a.PostScaleDrawOrder.CompareTo(b.PostScaleDrawOrder));

            foreach (var postScaleDrawable in postScaleDrawables)
            {
                postScaleDrawable.DrawPostScale(_spriteBatch, scaleFactor, renderOffset);
            }
        }

        private void CollectPostScaleDrawables(object component, List<IPostScaleDrawable> drawables, HashSet<IPostScaleDrawable> seen)
        {
            if (component is IPostScaleDrawable postScaleDrawable && seen.Add(postScaleDrawable))
            {
                drawables.Add(postScaleDrawable);
            }

            // Recursively check child controls
            if (component is XNAControls.IXNAControl xnaControl)
            {
                foreach (var child in xnaControl.ChildControls)
                {
                    CollectPostScaleDrawables(child, drawables, seen);
                }
            }
        }
    }
}
