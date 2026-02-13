using System;
using System.Collections.Generic;
using System.Diagnostics;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EOLib.Config;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using Optional;

namespace EndlessClient.Rendering.Chat
{
    /// <summary>
    /// Simple code-drawn chat bubble that renders text with a white background.
    /// Uses direct drawing instead of the complex 9-patch tile system for reliability.
    /// </summary>
    public class ChatBubble : DrawableGameComponent, IChatBubble, IPostScaleDrawable
    {
        private const int Padding = 8;        // Padding around text inside bubble
        private const int MaxTextWidth = 96;  // Max width before wrapping
        private const int NubHeight = 6;      // Height of the speech bubble "nub" pointing at character

        private readonly IMapActor _parent;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IContentProvider _contentProvider;
        private readonly SpriteBatch _spriteBatch;




        // Simple white pixel for drawing shapes
        private Texture2D _whitePixel;

        // Text state
        private string _message = string.Empty;
        private List<string> _wrappedLines = new List<string>();
        private float _textWidth;
        private float _textHeight;

        private bool _isGroupChat;
        private Vector2 _bubblePosition;  // Top-left of bubble
        private Option<Stopwatch> _startTime;

        // IPostScaleDrawable implementation
        public int PostScaleDrawOrder => 5; // Below HUD panels (0+), buttons (50), dialogs (100)
        public bool SkipRenderTargetDraw => true;

        public ChatBubble(IMapActor referenceRenderer,
                          IChatBubbleTextureProvider chatBubbleTextureProvider,  // Keep for interface compatibility
                          IEndlessGameProvider gameProvider,
                          IConfigurationProvider configurationProvider,
                          IClientWindowSizeProvider clientWindowSizeProvider,
                          IContentProvider contentProvider)
            : base((Game)gameProvider.Game)
        {
            _parent = referenceRenderer;
            _configurationProvider = configurationProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _contentProvider = contentProvider;
            _spriteBatch = new SpriteBatch(((Game)gameProvider.Game).GraphicsDevice);

            _startTime = Option.None<Stopwatch>();
            DrawOrder = 5; // Below HUD panels and dialogs
            Visible = false;
        }

        public override void Initialize()
        {
            // Create a 1x1 white pixel for drawing shapes
            _whitePixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            base.Initialize();
        }

        public void SetMessage(string message, bool isGroupChat)
        {
            if (!_configurationProvider.ShowChatBubbles || !_parent.IsAlive)
                return;

            _isGroupChat = isGroupChat;
            _message = message;
            Visible = true;

            // Calculate text wrapping and dimensions
            WrapText();

            _startTime = Option.Some(Stopwatch.StartNew());
        }

        private void WrapText()
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];

            var words = _message.Split(' ');
            _wrappedLines.Clear();
            var currentLine = "";

            foreach (var word in words)
            {
                // Handle words that are too long - break them with hyphens
                var wordToProcess = word;
                while (font.MeasureString(wordToProcess).Width > MaxTextWidth)
                {
                    var fitting = "";
                    for (int i = 0; i < wordToProcess.Length; i++)
                    {
                        var test = wordToProcess.Substring(0, i + 1) + "-";
                        if (font.MeasureString(test).Width > MaxTextWidth)
                            break;
                        fitting = wordToProcess.Substring(0, i + 1);
                    }

                    if (fitting.Length == 0)
                        fitting = wordToProcess.Substring(0, 1);

                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        _wrappedLines.Add(currentLine);
                        currentLine = "";
                    }
                    _wrappedLines.Add(fitting + "-");
                    wordToProcess = wordToProcess.Substring(fitting.Length);
                }

                // Normal word wrapping
                var testLine = string.IsNullOrEmpty(currentLine) ? wordToProcess : currentLine + " " + wordToProcess;

                if (font.MeasureString(testLine).Width > MaxTextWidth && !string.IsNullOrEmpty(currentLine))
                {
                    _wrappedLines.Add(currentLine);
                    currentLine = wordToProcess;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                _wrappedLines.Add(currentLine);

            // Calculate actual text dimensions from wrapped lines
            _textWidth = 0;
            foreach (var line in _wrappedLines)
            {
                var lineWidth = font.MeasureString(line).Width;
                if (lineWidth > _textWidth)
                    _textWidth = lineWidth;
            }
            _textHeight = _wrappedLines.Count * font.LineHeight;
        }

        public void Hide()
        {
            Visible = false;
        }

        public void Show()
        {
            Visible = _parent.IsAlive && _startTime.HasValue;
        }

        public override void Update(GameTime gameTime)
        {
            // Compute bubble position in render-target coordinates (640x480)
            // DrawPostScale() will convert these to screen-space
            var bubbleWidth = (int)_textWidth + Padding * 2;
            var bubbleHeight = _textHeight + Padding * 2;

            // Position bubble centered above character
            var bubbleX = _parent.HorizontalCenter - bubbleWidth / 2.0f;
            var bubbleY = _parent.NameLabelY - bubbleHeight - NubHeight + 10;

            // Apply zoom if needed - zoom the character anchor point, then offset by bubble size
            var zoom = _configurationProvider.MapZoom;
            if (zoom != 1.0f)
            {
                var centerX = _clientWindowSizeProvider.GameWidth / 2f;
                var centerY = _clientWindowSizeProvider.GameHeight / 2f;
                var zoomedCenterX = (_parent.HorizontalCenter - centerX) * zoom + centerX;
                var zoomedNameY = (_parent.NameLabelY - centerY) * zoom + centerY;
                bubbleX = zoomedCenterX - bubbleWidth / 2.0f;
                bubbleY = zoomedNameY - bubbleHeight - NubHeight + 10;
            }

            _bubblePosition = new Vector2((int)bubbleX, (int)bubbleY);

            // Auto-hide after timeout
            _startTime.MatchSome(st =>
            {
                if (st.ElapsedMilliseconds > (24 + _message.Length / 3) * 120)
                {
                    Visible = false;
                    _startTime = Option.None<Stopwatch>();
                }
            });

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            // Drawing is handled by DrawPostScale for crisp rendering at any scale
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (_wrappedLines.Count == 0 || !Visible)
                return;

            // Pick the best available font for this scale factor
            var font = FontScaleHelper.GetScaledFont(_contentProvider, scaleFactor);

            // Re-wrap text with the scaled font for correct line breaks
            var scaledMaxTextWidth = (int)(MaxTextWidth * scaleFactor);
            var scaledWrappedLines = WrapTextForFont(font, scaledMaxTextWidth);
            if (scaledWrappedLines.Count == 0)
                return;

            // Measure text dimensions with the scaled font
            float scaledTextWidth = 0;
            foreach (var line in scaledWrappedLines)
            {
                var lineWidth = font.MeasureString(line).Width;
                if (lineWidth > scaledTextWidth)
                    scaledTextWidth = lineWidth;
            }
            var scaledTextHeight = scaledWrappedLines.Count * font.LineHeight;

            // Scale padding and margins
            var scaledPadding = (int)(Padding * scaleFactor);

            var bubbleWidth = (int)scaledTextWidth + scaledPadding * 2;
            var bubbleHeight = (int)scaledTextHeight + scaledPadding * 2;

            // Convert render-target-space position to screen-space
            var screenX = (int)(_bubblePosition.X * scaleFactor) + renderOffset.X;
            var screenY = (int)(_bubblePosition.Y * scaleFactor) + renderOffset.Y;

            // Re-center the bubble horizontally around the scaled character center
            // (_bubblePosition.X was set to center the bubble at 1x scale, so we need to re-center for new width)
            var originalBubbleWidthScaled = (int)(_textWidth + Padding * 2) * scaleFactor;
            var centerX = screenX + (int)(originalBubbleWidthScaled / 2);
            screenX = centerX - bubbleWidth / 2;

            var scaledNubHeight = (int)(NubHeight * scaleFactor);

            // Colors
            var bubbleColor = _isGroupChat
                ? Color.FromNonPremultiplied(247, 234, 164, 232)
                : Color.FromNonPremultiplied(255, 255, 255, 232);
            var borderColor = Color.FromNonPremultiplied(0, 0, 0, 200);

            var bubbleRect = new Rectangle(screenX, screenY, bubbleWidth, bubbleHeight);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Draw bubble background
            _spriteBatch.Draw(_whitePixel, bubbleRect, bubbleColor);

            // Draw border (1px on each side)
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.X, bubbleRect.Y, bubbleRect.Width, 1), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.X, bubbleRect.Bottom - 1, bubbleRect.Width, 1), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.X, bubbleRect.Y, 1, bubbleRect.Height), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.Right - 1, bubbleRect.Y, 1, bubbleRect.Height), borderColor);

            // Draw nub (small triangle pointing down at character)
            var nubBaseWidth = (int)(8 * scaleFactor);
            var nubX = bubbleRect.X + bubbleRect.Width / 2 - nubBaseWidth / 2;
            var nubY = bubbleRect.Bottom;
            for (int i = 0; i < scaledNubHeight; i++)
            {
                var progress = (float)i / scaledNubHeight;
                var nubWidth = (int)(nubBaseWidth * (1 - progress));
                if (nubWidth > 0)
                {
                    var offset = (nubBaseWidth - nubWidth) / 2;
                    _spriteBatch.Draw(_whitePixel, new Rectangle(nubX + offset, nubY + i, nubWidth, 1), bubbleColor);
                }
            }

            // Draw text inside bubble
            var lineHeight = font.LineHeight;
            var textX = bubbleRect.X + scaledPadding;
            var textY = bubbleRect.Y + scaledPadding;
            for (int i = 0; i < scaledWrappedLines.Count; i++)
            {
                var linePos = new Vector2(textX, textY + i * lineHeight);
                _spriteBatch.DrawString(font, scaledWrappedLines[i], linePos, Color.Black);
            }

            _spriteBatch.End();
        }

        /// <summary>
        /// Wraps the current message for a specific font and max width.
        /// Used by DrawPostScale to re-wrap text for the scaled font.
        /// </summary>
        private List<string> WrapTextForFont(BitmapFont font, int maxWidth)
        {
            var lines = new List<string>();
            var words = _message.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var wordToProcess = word;
                while (font.MeasureString(wordToProcess).Width > maxWidth)
                {
                    var fitting = "";
                    for (int i = 0; i < wordToProcess.Length; i++)
                    {
                        var test = wordToProcess.Substring(0, i + 1) + "-";
                        if (font.MeasureString(test).Width > maxWidth)
                            break;
                        fitting = wordToProcess.Substring(0, i + 1);
                    }

                    if (fitting.Length == 0)
                        fitting = wordToProcess.Substring(0, 1);

                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = "";
                    }
                    lines.Add(fitting + "-");
                    wordToProcess = wordToProcess.Substring(fitting.Length);
                }

                var testLine = string.IsNullOrEmpty(currentLine) ? wordToProcess : currentLine + " " + wordToProcess;

                if (font.MeasureString(testLine).Width > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = wordToProcess;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Game.Components != null && Game.Components.Contains(this))
                    Game.Components.Remove(this);

                _whitePixel?.Dispose();
            }
        }
    }

    public interface IChatBubble : IDisposable
    {
        bool Visible { get; }

        void SetMessage(string message, bool isGroupChat);

        void Hide();

        void Show();
    }
}
