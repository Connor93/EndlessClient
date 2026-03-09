using EndlessClient.Dialogs;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace EndlessClient.UI.Myra
{
    /// <summary>
    /// Manages the Myra UI system lifecycle: initialization, rendering, and Desktop access.
    /// Acts as the single entry point for Myra integration within the game.
    /// </summary>
    public interface IMyraUIManager
    {
        /// <summary>
        /// The Myra Desktop instance that all Myra widgets are added to.
        /// </summary>
        Desktop Desktop { get; }

        /// <summary>
        /// Optional callback invoked immediately after Desktop.Render().
        /// Use this to draw XNA content on top of Myra windows (e.g. character previews).
        /// Set to null when no longer needed.
        /// </summary>
        System.Action PostRenderOverlay { get; set; }

        /// <summary>
        /// Initialize Myra (MyraEnvironment, Desktop, text input, stylesheet).
        /// Call from EndlessGame.LoadContent().
        /// </summary>
        void Initialize(Game game);

        /// <summary>
        /// Render all Myra widgets. Call at the end of EndlessGame.Draw().
        /// </summary>
        void Render();

        /// <summary>
        /// Update all registered Myra dialog adapters. Call from EndlessGame.Update().
        /// </summary>
        void UpdateDialogs(GameTime gameTime);

        /// <summary>
        /// Register a MyraDialogAdapter so it receives Update() calls.
        /// Called automatically by MyraDialogAdapter.Show().
        /// </summary>
        void RegisterDialog(MyraDialogAdapter dialog);

        /// <summary>
        /// Unregister a MyraDialogAdapter so it no longer receives Update() calls.
        /// Called automatically by MyraDialogAdapter.Close()/Dispose().
        /// </summary>
        void UnregisterDialog(MyraDialogAdapter dialog);

        /// <summary>
        /// Get the current mouse position in game-logical coordinates.
        /// Converts raw screen mouse position by subtracting the render offset
        /// (for letterbox/pillarbox centering) and dividing by the scale factor.
        /// Use this whenever positioning Myra widgets relative to the cursor.
        /// </summary>
        Vector2 GetLogicalMousePosition();

        /// <summary>
        /// Returns true when the mouse cursor is positioned over any visible Myra widget.
        /// Use this to prevent game-world mouse interactions (e.g. walking) from firing
        /// when the player is clicking on a Myra UI element.
        /// </summary>
        bool IsMouseOverGUI();
    }
}
