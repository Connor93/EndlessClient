using System;
using System.Collections.Generic;
using System.IO;
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
            System.IO.File.AppendAllText("debug_log.txt", "EndlessGame Constructor START\n");
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

            // setting Width/Height in window size repository applies the change to disable vsync
            _graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            _graphicsDeviceManager.IsFullScreen = false;
            _windowSizeRepository.Width = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH;
            _windowSizeRepository.Height = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT;

            _windowSizeRepository.GameWindowSizeChanged += (_, _) =>
            {
                if (_windowSizeRepository.Width < ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT)
                    _windowSizeRepository.Width = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH;

                if (_windowSizeRepository.Height < ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT)
                    _windowSizeRepository.Height = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT;

                // Recreate the game render target if we're in scaled mode and dimensions changed
                if (_windowSizeRepository.IsScaledMode && _gameRenderTarget != null)
                {
                    var newWidth = _windowSizeRepository.GameWidth;
                    var newHeight = _windowSizeRepository.GameHeight;
                    if (_gameRenderTarget.Width != newWidth || _gameRenderTarget.Height != newHeight)
                    {
                        System.Console.WriteLine($"[SCALED MODE] Recreating render target: {_gameRenderTarget.Width}x{_gameRenderTarget.Height} -> {newWidth}x{newHeight}");
                        _gameRenderTarget.Dispose();
                        _gameRenderTarget = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(
                            GraphicsDevice,
                            newWidth,
                            newHeight,
                            false,
                            Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color,
                            Microsoft.Xna.Framework.Graphics.DepthFormat.None);
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

            // Initialize scaled client mode if configured
            if (_configurationProvider.ScaledClient)
            {
                _windowSizeRepository.IsScaledMode = true;

                // Set configured game dimensions for when player logs in (0 = use default 640x480)
                _windowSizeRepository.ConfiguredGameWidth = _configurationProvider.InGameWidth;
                _windowSizeRepository.ConfiguredGameHeight = _configurationProvider.InGameHeight;

                // Enable window resizing for scaling
                Window.AllowUserResizing = true;

                // Start at 640x480 for pre-login screens (IsInGame is false)
                // Game dimensions will switch when player enters the game world
                var startWidth = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_WIDTH;
                var startHeight = ClientWindowSizeRepository.DEFAULT_BACKBUFFER_HEIGHT;

                System.Console.WriteLine($"[SCALED MODE] Config InGameWidth={_configurationProvider.InGameWidth}, InGameHeight={_configurationProvider.InGameHeight}");
                System.Console.WriteLine($"[SCALED MODE] Starting at {startWidth}x{startHeight} (pre-login)");

                _gameRenderTarget = new RenderTarget2D(
                    GraphicsDevice,
                    startWidth,
                    startHeight,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None);
            }

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
            // Force updates to wait every 12ms
            // Some game components rely on slower update times. 60FPS was the original, but 12ms factors nicely in 120ms "ticks"
            // See: https://github.com/ethanmoffat/EndlessClient/issues/199
            // Using IsFixedTimeStep = true with TargetUpdateTime set to 60FPS also limits the draw rate, which is not desired
            if ((gameTime.TotalGameTime - _lastFrameUpdate).TotalMilliseconds >= FixedTimeStepRepository.TICK_TIME_MS)
            {
#if DEBUG
                var currentKeyState = Keyboard.GetState();
                if (KeyboardExtended.GetState().WasKeyJustDown(Keys.F5))
                {
                    _testModeLauncher.LaunchTestMode();
                }

                _previousKeyState = currentKeyState;
#endif
                _fixedTimeStepRepository.Tick();

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

                _lastFrameUpdate = gameTime.TotalGameTime;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            var isTestMode = _controlSetRepository.CurrentControlSet.GameState == GameStates.TestMode;

            // Use render target scaling when in scaled mode (both pre-login and in-game)
            // XNAControls InputManager now supports coordinate transformation for correct hit detection
            if (_windowSizeRepository.IsScaledMode && _gameRenderTarget != null)
            {
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
            }
            else
            {
                // Normal rendering path (no scaling) - used for in-game floating layout
                GraphicsDevice.Clear(isTestMode ? Color.White : Color.Black);
                base.Draw(gameTime);
            }

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
            // Find all post-scale drawable controls (including nested children) and sort by draw order
            // Lower order values are drawn first (background), higher values are drawn later (foreground)
            var postScaleDrawables = new List<IPostScaleDrawable>();
            foreach (var component in Components)
            {
                CollectPostScaleDrawables(component, postScaleDrawables);
            }

            // Sort by PostScaleDrawOrder - lower values first, dialogs (100) on top of HUD panels (0)
            postScaleDrawables.Sort((a, b) => a.PostScaleDrawOrder.CompareTo(b.PostScaleDrawOrder));

            foreach (var postScaleDrawable in postScaleDrawables)
            {
                postScaleDrawable.DrawPostScale(_spriteBatch, scaleFactor, renderOffset);
            }
        }

        private void CollectPostScaleDrawables(object component, List<IPostScaleDrawable> drawables)
        {
            if (component is IPostScaleDrawable postScaleDrawable)
            {
                drawables.Add(postScaleDrawable);
            }

            // Recursively check child controls
            if (component is XNAControls.IXNAControl xnaControl)
            {
                foreach (var child in xnaControl.ChildControls)
                {
                    CollectPostScaleDrawables(child, drawables);
                }
            }
        }
    }
}
