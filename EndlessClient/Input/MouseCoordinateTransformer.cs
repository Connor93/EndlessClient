using System;
using EndlessClient.Rendering;
using Microsoft.Xna.Framework;
using XNAControls.Input;

namespace EndlessClient.Input
{
    /// <summary>
    /// Transforms mouse coordinates from window space to game space for scaled rendering.
    /// When the game renders at a fixed resolution (e.g., 1280x720) and scales up to fill
    /// the window, this transformer converts window mouse coordinates to game coordinates.
    /// </summary>
    public class MouseCoordinateTransformer : IMouseCoordinateTransformer
    {
        private readonly IClientWindowSizeProvider _windowSizeProvider;

        public MouseCoordinateTransformer(IClientWindowSizeProvider windowSizeProvider)
        {
            _windowSizeProvider = windowSizeProvider;
        }

        public Point TransformMousePosition(Point windowPosition)
        {
            // If not in scaled mode, return position unchanged
            if (!_windowSizeProvider.IsScaledMode)
            {
                return windowPosition;
            }

            var offset = _windowSizeProvider.RenderOffset;
            var scale = _windowSizeProvider.ScaleFactor;

            // Transform from window coordinates to game coordinates
            int gameX = (int)((windowPosition.X - offset.X) / scale);
            int gameY = (int)((windowPosition.Y - offset.Y) / scale);

            // Clamp to game bounds
            gameX = Math.Clamp(gameX, 0, _windowSizeProvider.GameWidth - 1);
            gameY = Math.Clamp(gameY, 0, _windowSizeProvider.GameHeight - 1);

            return new Point(gameX, gameY);
        }
    }
}
