using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.Rendering
{
    /// <summary>
    /// Marker interface for controls that should be drawn after the game world is scaled.
    /// These controls render directly to the backbuffer at native window resolution,
    /// appearing crisp regardless of the window scale factor.
    /// </summary>
    public interface IPostScaleDrawable
    {
        /// <summary>
        /// Draws the control to the backbuffer after render target scaling.
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch to use for drawing</param>
        /// <param name="scaleFactor">Current window scale factor</param>
        /// <param name="renderOffset">Letterbox/pillarbox offset from render target blit</param>
        void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset);

        /// <summary>
        /// Gets whether this control should skip normal drawing during render target phase.
        /// </summary>
        bool SkipRenderTargetDraw { get; }

        /// <summary>
        /// Gets the draw order for post-scale rendering. Higher values are drawn later (on top).
        /// Recommended values: 0 = HUD panels, 100 = Dialogs, 200 = Tooltips/Overlays
        /// </summary>
        int PostScaleDrawOrder { get; }
    }
}
