using System;
using EndlessClient.Rendering;
using Microsoft.Xna.Framework;
using XNAControls.Input;

namespace EndlessClient.Input
{
    /// <summary>
    /// Transforms mouse coordinates from window space to game space for scaled rendering.
    /// The game renders at a fixed resolution and scales up to fill the window.
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

        public bool IsInBounds(Point windowPosition)
        {
            // Check if the window position is within the scaled game area
            var offset = _windowSizeProvider.RenderOffset;
            var scale = _windowSizeProvider.ScaleFactor;
            int scaledWidth = (int)(_windowSizeProvider.GameWidth * scale);
            int scaledHeight = (int)(_windowSizeProvider.GameHeight * scale);

            return windowPosition.X >= offset.X && windowPosition.X < offset.X + scaledWidth &&
                   windowPosition.Y >= offset.Y && windowPosition.Y < offset.Y + scaledHeight;
        }
    }
}
