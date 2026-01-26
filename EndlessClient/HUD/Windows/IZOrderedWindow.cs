using System;
using EndlessClient.Rendering;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Interface for windows that support dynamic z-ordering.
    /// Windows implementing this interface can be brought to front when activated.
    /// </summary>
    public interface IZOrderedWindow : IPostScaleDrawable
    {
        /// <summary>
        /// Event fired when the window is activated (clicked, focused).
        /// </summary>
        event Action Activated;

        /// <summary>
        /// Brings this window to the front of all other z-ordered windows.
        /// </summary>
        void BringToFront();

        /// <summary>
        /// Gets or sets the current z-order value for this window.
        /// Higher values are drawn on top.
        /// </summary>
        int ZOrder { get; set; }
    }
}
