using EndlessClient.HUD.Panels;
using EOLib.Domain.Chat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.Rendering.Chat
{
    public interface IChatRenderable
    {
        int DisplayIndex { get; set; }

        ChatData Data { get; }

        void Render(IHudPanel parentPanel, SpriteBatch spriteBatch, BitmapFont chatFont);

        /// <summary>
        /// Renders with scissor clipping enabled for text overflow prevention
        /// </summary>
        void RenderWithClipping(IHudPanel parentPanel, SpriteBatch spriteBatch, BitmapFont chatFont);

        /// <summary>
        /// Renders at a specific scaled position for post-scale crisp text
        /// </summary>
        void RenderScaled(SpriteBatch spriteBatch, BitmapFont chatFont, Vector2 scaledPosition, float scale);

        /// <summary>
        /// Renders at a specific scaled position with scissor clipping enabled
        /// </summary>
        void RenderScaledWithClipping(SpriteBatch spriteBatch, BitmapFont chatFont, Vector2 scaledPosition, float scale);
    }
}
