using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.UI.Controls
{
    /// <summary>
    /// Utility class for procedurally drawing UI shapes
    /// </summary>
    public static class DrawingPrimitives
    {
        private static Texture2D _pixel;

        /// <summary>
        /// Initialize the drawing primitives with a graphics device
        /// </summary>
        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            if (_pixel == null || _pixel.IsDisposed)
            {
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
        }

        /// <summary>
        /// Draw a filled rectangle
        /// </summary>
        public static void DrawFilledRect(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            if (_pixel == null) return;
            spriteBatch.Draw(_pixel, rect, color);
        }

        /// <summary>
        /// Draw a rectangle border (outline only)
        /// </summary>
        public static void DrawRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness = 1)
        {
            if (_pixel == null) return;

            // Top
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        /// <summary>
        /// Draw a rounded rectangle (filled) - uses corner approximation
        /// </summary>
        public static void DrawRoundedRect(SpriteBatch spriteBatch, Rectangle rect, Color color, int cornerRadius)
        {
            if (_pixel == null || cornerRadius <= 0)
            {
                DrawFilledRect(spriteBatch, rect, color);
                return;
            }

            cornerRadius = Math.Min(cornerRadius, Math.Min(rect.Width, rect.Height) / 2);

            // Main center rectangle (full height, inset by corner radius)
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.X + cornerRadius, rect.Y,
                rect.Width - cornerRadius * 2, rect.Height), color);

            // Left side (inset vertically)
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.X, rect.Y + cornerRadius,
                cornerRadius, rect.Height - cornerRadius * 2), color);

            // Right side (inset vertically)
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.Right - cornerRadius, rect.Y + cornerRadius,
                cornerRadius, rect.Height - cornerRadius * 2), color);

            // Draw corners as circular approximation
            DrawCorner(spriteBatch, rect.X, rect.Y, cornerRadius, color, Corner.TopLeft);
            DrawCorner(spriteBatch, rect.Right - cornerRadius, rect.Y, cornerRadius, color, Corner.TopRight);
            DrawCorner(spriteBatch, rect.X, rect.Bottom - cornerRadius, cornerRadius, color, Corner.BottomLeft);
            DrawCorner(spriteBatch, rect.Right - cornerRadius, rect.Bottom - cornerRadius, cornerRadius, color, Corner.BottomRight);
        }

        /// <summary>
        /// Draw a rounded rectangle border
        /// </summary>
        public static void DrawRoundedRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int cornerRadius, int thickness = 1)
        {
            if (_pixel == null || cornerRadius <= 0)
            {
                DrawRectBorder(spriteBatch, rect, color, thickness);
                return;
            }

            cornerRadius = Math.Min(cornerRadius, Math.Min(rect.Width, rect.Height) / 2);

            // Top edge
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.X + cornerRadius, rect.Y,
                rect.Width - cornerRadius * 2, thickness), color);

            // Bottom edge
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.X + cornerRadius, rect.Bottom - thickness,
                rect.Width - cornerRadius * 2, thickness), color);

            // Left edge
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.X, rect.Y + cornerRadius,
                thickness, rect.Height - cornerRadius * 2), color);

            // Right edge
            spriteBatch.Draw(_pixel, new Rectangle(
                rect.Right - thickness, rect.Y + cornerRadius,
                thickness, rect.Height - cornerRadius * 2), color);

            // Corner arcs (approximated)
            DrawCornerArc(spriteBatch, rect.X, rect.Y, cornerRadius, color, thickness, Corner.TopLeft);
            DrawCornerArc(spriteBatch, rect.Right - cornerRadius, rect.Y, cornerRadius, color, thickness, Corner.TopRight);
            DrawCornerArc(spriteBatch, rect.X, rect.Bottom - cornerRadius, cornerRadius, color, thickness, Corner.BottomLeft);
            DrawCornerArc(spriteBatch, rect.Right - cornerRadius, rect.Bottom - cornerRadius, cornerRadius, color, thickness, Corner.BottomRight);
        }

        private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

        private static void DrawCorner(SpriteBatch spriteBatch, int x, int y, int radius, Color color, Corner corner)
        {
            // Draw a filled quarter circle using horizontal scanlines
            for (int row = 0; row < radius; row++)
            {
                // Calculate the width of this row based on circle equation
                double angle = Math.Acos((double)(radius - row) / radius);
                int width = (int)(radius - radius * Math.Cos(angle + Math.PI / 2));

                int drawX = x;
                int drawY = y + row;
                int drawWidth = width;

                switch (corner)
                {
                    case Corner.TopLeft:
                        drawX = x + (radius - width);
                        break;
                    case Corner.TopRight:
                        drawX = x;
                        break;
                    case Corner.BottomLeft:
                        drawX = x + (radius - width);
                        drawY = y + (radius - row - 1);
                        break;
                    case Corner.BottomRight:
                        drawX = x;
                        drawY = y + (radius - row - 1);
                        break;
                }

                if (drawWidth > 0)
                    spriteBatch.Draw(_pixel, new Rectangle(drawX, drawY, drawWidth, 1), color);
            }
        }

        private static void DrawCornerArc(SpriteBatch spriteBatch, int x, int y, int radius, Color color, int thickness, Corner corner)
        {
            // Draw quarter circle arc using pixel approximation
            int steps = Math.Max(8, radius);
            for (int i = 0; i <= steps; i++)
            {
                double angle = (Math.PI / 2) * i / steps;
                int px = (int)(radius * Math.Cos(angle));
                int py = (int)(radius * Math.Sin(angle));

                int drawX = x, drawY = y;
                switch (corner)
                {
                    case Corner.TopLeft:
                        drawX = x + radius - px;
                        drawY = y + radius - py;
                        break;
                    case Corner.TopRight:
                        drawX = x + px;
                        drawY = y + radius - py;
                        break;
                    case Corner.BottomLeft:
                        drawX = x + radius - px;
                        drawY = y + py;
                        break;
                    case Corner.BottomRight:
                        drawX = x + px;
                        drawY = y + py;
                        break;
                }

                spriteBatch.Draw(_pixel, new Rectangle(drawX, drawY, thickness, thickness), color);
            }
        }
    }
}
