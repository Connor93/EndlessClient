using System;
using System.Collections.Generic;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.HUD.Toast
{
    /// <summary>
    /// Manages toast notifications with slide-in/fade-out animations.
    /// Implements IPostScaleDrawable for crisp text rendering when scaled.
    /// Only shows notifications for specific important events.
    /// </summary>
    public class CodeDrawnToastManager : DrawableGameComponent, IPostScaleDrawable
    {
        private const int MaxVisibleToasts = 3;
        private const int ToastDisplayTimeMs = 3000;
        private const int SlideInTimeMs = 200;
        private const int FadeOutTimeMs = 300;
        private const int ToastWidth = 280;
        private const int ToastHeight = 28;
        private const int ToastSpacing = 4;
        private const int ScreenPadding = 10;

        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IContentProvider _contentProvider;

        private readonly List<ToastNotification> _toasts = new List<ToastNotification>();
        private readonly SpriteBatch _spriteBatch;
        private Texture2D _whitePixel;

        // IPostScaleDrawable implementation
        public int PostScaleDrawOrder => 250; // Above dialogs/tooltips
        public bool SkipRenderTargetDraw => false;

        public CodeDrawnToastManager(
            IEndlessGameProvider gameProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IUIStyleProvider styleProvider,
            IContentProvider contentProvider)
            : base((Game)gameProvider.Game)
        {
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _styleProvider = styleProvider;
            _contentProvider = contentProvider;

            _spriteBatch = new SpriteBatch(((Game)gameProvider.Game).GraphicsDevice);
            DrawOrder = 1000; // Very high to be on top
        }

        public override void Initialize()
        {
            _whitePixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });
            base.Initialize();
        }

        #region IToastNotifier Implementation

        public void NotifyExpGained(int amount)
        {
            if (!_clientWindowSizeProvider.Resizable)
                return;

            AddToast($"+{amount} EXP", ToastType.Action);
        }

        public void NotifyItemPickup(string itemName, int amount)
        {
            if (!_clientWindowSizeProvider.Resizable)
                return;

            var message = amount > 1 ? $"Picked up {amount} {itemName}" : $"Picked up {itemName}";
            AddToast(message, ToastType.Action);
        }

        public void NotifyItemDrop(string itemName, int amount)
        {
            if (!_clientWindowSizeProvider.Resizable)
                return;

            var message = amount > 1 ? $"Dropped {amount} {itemName}" : $"Dropped {itemName}";
            AddToast(message, ToastType.Info);
        }

        public void NotifyNPCDrop(string playerName, string itemName, int amount)
        {
            if (!_clientWindowSizeProvider.Resizable)
                return;

            var amountText = amount > 1 ? $"{amount} {itemName}" : itemName;
            var message = $"{playerName} got {amountText}";
            AddToast(message, ToastType.Action);
        }

        #endregion

        private void AddToast(string message, ToastType type)
        {
            var toast = new ToastNotification(message, type);
            _toasts.Insert(0, toast);

            // Remove oldest if over limit
            while (_toasts.Count > MaxVisibleToasts)
            {
                _toasts.RemoveAt(_toasts.Count - 1);
            }
        }

        public override void Update(GameTime gameTime)
        {
            // Update existing toasts
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var toast = _toasts[i];
                var elapsed = toast.Timer.ElapsedMilliseconds;

                // Slide in animation
                if (toast.SlideProgress < 1)
                {
                    toast.SlideProgress = Math.Min(1, elapsed / (float)SlideInTimeMs);
                }

                // Start fade out after display time
                if (elapsed > ToastDisplayTimeMs)
                {
                    var fadeElapsed = elapsed - ToastDisplayTimeMs;
                    toast.FadeProgress = Math.Min(1, fadeElapsed / (float)FadeOutTimeMs);

                    if (toast.FadeProgress >= 1)
                    {
                        toast.IsExpired = true;
                    }
                }
            }

            // Remove expired toasts
            _toasts.RemoveAll(t => t.IsExpired);

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            // Skip drawing in scaled mode - use DrawPostScale instead
            if (_clientWindowSizeProvider.IsScaledMode)
                return;

            // Not used in scaled mode, but draw normally for non-scaled
            DrawToasts(_spriteBatch, 1f, Point.Zero, false);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!_clientWindowSizeProvider.IsScaledMode || _toasts.Count == 0)
                return;

            DrawToasts(spriteBatch, scaleFactor, renderOffset, true);
        }

        private void DrawToasts(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset, bool isPostScale)
        {
            if (_toasts.Count == 0)
                return;

            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            var screenWidth = isPostScale
                ? (int)(_clientWindowSizeProvider.GameWidth * scaleFactor) + renderOffset.X * 2
                : _clientWindowSizeProvider.Width;

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            for (int i = 0; i < _toasts.Count; i++)
            {
                var toast = _toasts[i];

                // Calculate position with slide animation
                var slideOffset = (1 - EaseOutQuad(toast.SlideProgress)) * (ToastWidth + ScreenPadding);
                var targetX = screenWidth - ToastWidth - ScreenPadding + slideOffset;
                var targetY = ScreenPadding + i * (ToastHeight + ToastSpacing);

                if (isPostScale)
                {
                    targetX = _clientWindowSizeProvider.GameWidth * scaleFactor + renderOffset.X - ToastWidth - ScreenPadding + slideOffset;
                    targetY = renderOffset.Y + ScreenPadding + i * (ToastHeight + ToastSpacing);
                }

                // Calculate alpha for fade
                var alpha = 1 - toast.FadeProgress;

                // Get colors based on type
                GetToastColors(toast.Type, out var bgColor, out var borderColor);
                bgColor = bgColor * alpha;
                borderColor = borderColor * alpha;
                var textColor = _styleProvider.TextPrimary * alpha;

                // Draw background
                var toastRect = new Rectangle((int)targetX, (int)targetY, ToastWidth, ToastHeight);
                _spriteBatch.Draw(_whitePixel, toastRect, bgColor);

                // Draw border
                _spriteBatch.Draw(_whitePixel, new Rectangle(toastRect.X, toastRect.Y, toastRect.Width, 1), borderColor);
                _spriteBatch.Draw(_whitePixel, new Rectangle(toastRect.X, toastRect.Bottom - 1, toastRect.Width, 1), borderColor);
                _spriteBatch.Draw(_whitePixel, new Rectangle(toastRect.X, toastRect.Y, 1, toastRect.Height), borderColor);
                _spriteBatch.Draw(_whitePixel, new Rectangle(toastRect.Right - 1, toastRect.Y, 1, toastRect.Height), borderColor);

                // Truncate text if needed
                var text = TruncateText(toast.Message, font, ToastWidth - 16);
                var textPos = new Vector2(toastRect.X + 8, toastRect.Y + (ToastHeight - font.LineHeight) / 2);

                _spriteBatch.DrawString(font, text, textPos, textColor);
            }

            _spriteBatch.End();
        }

        private void GetToastColors(ToastType type, out Color bgColor, out Color borderColor)
        {
            switch (type)
            {
                case ToastType.Warning:
                    bgColor = _styleProvider.ToastWarningBackground;
                    borderColor = _styleProvider.ToastWarningBorder;
                    break;
                case ToastType.Action:
                    bgColor = _styleProvider.ToastActionBackground;
                    borderColor = _styleProvider.ToastActionBorder;
                    break;
                default:
                    bgColor = _styleProvider.ToastInfoBackground;
                    borderColor = _styleProvider.ToastInfoBorder;
                    break;
            }
        }

        private static float EaseOutQuad(float t) => 1 - (1 - t) * (1 - t);

        private static string TruncateText(string text, BitmapFont font, int maxWidth)
        {
            if (font.MeasureString(text).Width <= maxWidth)
                return text;

            var truncated = text;
            while (truncated.Length > 0 && font.MeasureString(truncated + "...").Width > maxWidth)
            {
                truncated = truncated.Substring(0, truncated.Length - 1);
            }
            return truncated + "...";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _whitePixel?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
