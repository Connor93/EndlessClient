using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using EndlessClient.Dialogs;
using EndlessClient.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;

namespace EndlessClient.UI.Myra
{
    /// <summary>
    /// Owns the Myra Desktop and coordinates initialization, theming, and rendering.
    /// Registered as a singleton — EndlessGame receives this via DI and calls
    /// Initialize() from LoadContent() and Render() from Draw().
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class MyraUIManager : IMyraUIManager
    {
        private readonly IMyraStylesheetProvider _stylesheetProvider;
        private readonly IClientWindowSizeProvider _windowSizeProvider;

        private Desktop? _desktop;
        private SpriteBatch? _overlaySpriteBatch;
        private Texture2D? _overlayPixel;
        private readonly List<MyraDialogAdapter> _activeDialogs = new();

        public MyraUIManager(IMyraStylesheetProvider stylesheetProvider,
                             IClientWindowSizeProvider windowSizeProvider)
        {
            _stylesheetProvider = stylesheetProvider;
            _windowSizeProvider = windowSizeProvider;
        }

        public Desktop Desktop => _desktop ?? throw new System.InvalidOperationException(
            "MyraUIManager.Initialize() must be called before accessing the Desktop.");

        public void Initialize(Game game)
        {
            // Core Myra setup
            MyraEnvironment.Game = game;

            // Disable Myra's built-in modal darkening — it only covers Desktop.Bounds
            // which doesn't extend to the letterbox/pillarbox regions when scaled.
            // We draw our own full-viewport overlay instead (see Render()).
            MyraEnvironment.EnableModalDarkening = false;

            // Build and apply the DarkParchment stylesheet
            _stylesheetProvider.Rebuild();
            _stylesheetProvider.Apply();

            // Create the Desktop
            _desktop = new Desktop();

            // Forward text input from the MonoGame window
            // Myra needs this for text boxes — without it, Myra tries to translate
            // XNA Keys to characters itself, which doesn't handle all locales.
            _desktop.HasExternalTextInput = true;
            game.Window.TextInput += (_, a) =>
            {
                _desktop.OnChar(a.Character);
            };

            // Create resources for the full-viewport modal overlay
            _overlaySpriteBatch = new SpriteBatch(game.GraphicsDevice);
            _overlayPixel = new Texture2D(game.GraphicsDevice, 1, 1);
            _overlayPixel.SetData([Color.White]);
        }

        public System.Action PostRenderOverlay { get; set; }

        public void Render()
        {
            if (_desktop == null)
                return;

            // Apply the game's scale factor so Myra renders at game-logical coordinates.
            // Desktop.Scale affects both rendering AND mouse input (via the internal
            // inverse matrix used by ToLocal()). BoundsFetcher tells Myra the layout
            // area at the render offset — Scale handles the actual scaling separately.
            var scale = _windowSizeProvider.ScaleFactor;
            var offset = _windowSizeProvider.RenderOffset;

            _desktop.Scale = new Vector2(scale, scale);
            _desktop.BoundsFetcher = () => new Rectangle(
                offset.X, offset.Y,
                _windowSizeProvider.GameWidth,
                _windowSizeProvider.GameHeight);

            // If a modal dialog is showing, draw a full-viewport dark overlay first.
            // This covers the letterbox/pillarbox areas that Myra's built-in overlay misses.
            if (_desktop.Widgets.Any(w => w is Window { IsModal: true }))
            {
                var gd = MyraEnvironment.Game.GraphicsDevice;
                _overlaySpriteBatch!.Begin();
                _overlaySpriteBatch.Draw(_overlayPixel, new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height), Color.Black * 0.5f);
                _overlaySpriteBatch.End();
            }

            _desktop.Render();

            // Draw any registered overlays on top of Myra (e.g. character previews)
            PostRenderOverlay?.Invoke();
        }

        public void UpdateDialogs(GameTime gameTime)
        {
            // Snapshot to avoid issues if a dialog registers/unregisters during Update
            for (var i = _activeDialogs.Count - 1; i >= 0; i--)
            {
                _activeDialogs[i].Update(gameTime);
            }
        }

        public void RegisterDialog(MyraDialogAdapter dialog)
        {
            if (!_activeDialogs.Contains(dialog))
                _activeDialogs.Add(dialog);
        }

        public void UnregisterDialog(MyraDialogAdapter dialog)
        {
            _activeDialogs.Remove(dialog);
        }

        public Vector2 GetLogicalMousePosition()
        {
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var scale = _windowSizeProvider.ScaleFactor;
            var offset = _windowSizeProvider.RenderOffset;

            return new Vector2(
                (mouseState.X - offset.X) / scale,
                (mouseState.Y - offset.Y) / scale);
        }

        public bool IsMouseOverGUI()
        {
            if (_desktop == null) return false;

            var logicalPos = GetLogicalMousePosition();
            var point = new Point((int)logicalPos.X, (int)logicalPos.Y);

            return _desktop.IsPointOverGUI(point);
        }
    }
}
