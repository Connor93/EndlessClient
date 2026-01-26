using System;
using EndlessClient.HUD.Windows;
using EndlessClient.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Abstract base class for code-drawn HUD panels that support post-scale rendering.
    /// Provides shared implementation of IZOrderedWindow to eliminate boilerplate.
    /// </summary>
    public abstract class CodeDrawnHudPanelBase : DraggableHudPanel, IZOrderedWindow
    {
        private readonly IClientWindowSizeProvider _windowSizeProvider;
        private int _zOrder;

        /// <summary>
        /// Provides access to window size information for derived classes that need mouse position transformation.
        /// </summary>
        protected IClientWindowSizeProvider WindowSizeProvider => _windowSizeProvider;

        /// <summary>
        /// Gets whether to skip rendering during the render target phase.
        /// When true, drawing is deferred to DrawPostScale for crisp text at any scale.
        /// </summary>
        public bool SkipRenderTargetDraw => _windowSizeProvider.IsScaledMode;

        /// <summary>
        /// Gets the draw order for post-scale rendering. Uses ZOrder for proper stacking.
        /// </summary>
        public int PostScaleDrawOrder => _zOrder;

        /// <summary>
        /// Gets or sets the z-order for window stacking. Higher values render on top.
        /// </summary>
        int IZOrderedWindow.ZOrder { get => _zOrder; set => _zOrder = value; }

        /// <summary>
        /// Creates a new code-drawn HUD panel base.
        /// </summary>
        /// <param name="windowSizeProvider">Provider for window scaling information</param>
        protected CodeDrawnHudPanelBase(IClientWindowSizeProvider windowSizeProvider)
            : base(windowSizeProvider.Resizable)
        {
            _windowSizeProvider = windowSizeProvider;
        }

        /// <summary>
        /// Brings this window to the front. Z-order is managed externally by WindowZOrderManager.
        /// </summary>
        public virtual void BringToFront()
        {
            // Z-order is set externally by WindowZOrderManager
        }

        /// <summary>
        /// Handles draw dispatch between scaled and non-scaled modes.
        /// </summary>
        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                // In scaled mode: skip fills here - they will be drawn in DrawPostScale
                base.OnDrawControl(gameTime);
                return;
            }

            // Normal mode: draw everything in one pass
            DrawComplete(DrawPositionWithParentOffset);
            base.OnDrawControl(gameTime);
        }

        /// <summary>
        /// Draws the panel in post-scale phase for crisp rendering at any window size.
        /// </summary>
        public virtual void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var scaledPos = CalculateScaledPosition(scaleFactor, renderOffset);

            // Draw fills first, then borders/text for correct layering
            DrawFillsScaled(scaledPos, scaleFactor);
            DrawBordersAndTextScaled(scaledPos, scaleFactor);
        }

        /// <summary>
        /// Calculates the scaled screen position from game coordinates.
        /// </summary>
        protected Vector2 CalculateScaledPosition(float scaleFactor, Point renderOffset)
        {
            var gamePos = DrawPositionWithParentOffset;
            return new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);
        }

        /// <summary>
        /// Draws filled backgrounds at scaled coordinates. Called first in post-scale phase.
        /// </summary>
        protected abstract void DrawFillsScaled(Vector2 scaledPos, float scaleFactor);

        /// <summary>
        /// Draws borders and text at scaled coordinates. Called second in post-scale phase.
        /// </summary>
        protected abstract void DrawBordersAndTextScaled(Vector2 scaledPos, float scaleFactor);

        /// <summary>
        /// Draws everything in one pass for non-scaled mode.
        /// </summary>
        protected abstract void DrawComplete(Vector2 pos);
    }
}
