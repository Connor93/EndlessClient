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
        private const int ExtraWidthMargin = 30; // Extra width beyond MaxTextWidth to guarantee fit
        private const int ExtraHeightMargin = 12; // Extra height to prevent vertical clipping
        private const int MaxTextWidth = 96;  // Max width before wrapping
        private const int NubHeight = 6;      // Height of the speech bubble "nub" pointing at character

        private readonly IMapActor _parent;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IContentProvider _contentProvider;
        private readonly SpriteBatch _spriteBatch;

        // Scissor state for clipping text to bubble bounds
        private static readonly RasterizerState _scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };

        // Simple white pixel for drawing shapes
        private Texture2D _whitePixel;

        // Text state
        private string _message = string.Empty;
        private List<string> _wrappedLines = new List<string>();
        private float _textWidth;
        private float _textHeight;

        private bool _isGroupChat;
        private Vector2 _bubblePosition;  // Top-left of bubble
        private Vector2 _textPosition;    // Where text starts
        private Option<Stopwatch> _startTime;

        // IPostScaleDrawable implementation
        public bool SkipRenderTargetDraw => false;

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
            DrawOrder = 29;
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
            // Extra margins are only needed at 1:1 scale to compensate for font measurement issues
            // When scaled up, the base padding is sufficient and extra margins look excessive
            var isActuallyScaled = _clientWindowSizeProvider.IsScaledMode && _clientWindowSizeProvider.ScaleFactor > 1.0f;
            var effectivePadding = isActuallyScaled ? 4 : Padding;  // Use smaller padding when scaled
            var extraWidth = isActuallyScaled ? 0 : ExtraWidthMargin;
            var extraHeight = isActuallyScaled ? 0 : ExtraHeightMargin;

            // Use MaxTextWidth + extra margin to GUARANTEE text fits
            // Font measurement is unreliable, so we add significant extra width
            var bubbleWidth = MaxTextWidth + effectivePadding * 2 + extraWidth;
            var bubbleHeight = _textHeight + effectivePadding * 2 + extraHeight;

            // Position bubble centered above character
            var bubbleX = _parent.HorizontalCenter - bubbleWidth / 2.0f;
            var bubbleY = _parent.NameLabelY - bubbleHeight - NubHeight + 10;

            // Apply zoom if needed
            var zoom = _configurationProvider.MapZoom;
            if (zoom != 1.0f)
            {
                var centerX = _clientWindowSizeProvider.GameWidth / 2f;
                var centerY = _clientWindowSizeProvider.GameHeight / 2f;
                bubbleX = (_parent.HorizontalCenter - centerX) * zoom + centerX - bubbleWidth / 2.0f;
                bubbleY = (_parent.NameLabelY - bubbleHeight - NubHeight + 10 - centerY) * zoom + centerY;
            }

            // CRITICAL: Floor to integer to match what Draw() does when casting to Rectangle
            // This ensures text and bubble are on the same pixel grid
            _bubblePosition = new Vector2((int)bubbleX, (int)bubbleY);
            _textPosition = new Vector2((int)bubbleX + effectivePadding, (int)bubbleY + effectivePadding);

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
            if (_wrappedLines.Count == 0)
                return;

            var font = _contentProvider.Fonts[Constants.FontSize08pt5];

            // Use same conditional margin logic as Update()
            var isActuallyScaled = _clientWindowSizeProvider.IsScaledMode && _clientWindowSizeProvider.ScaleFactor > 1.0f;
            var effectivePadding = isActuallyScaled ? 4 : Padding;
            var extraWidth = isActuallyScaled ? 0 : ExtraWidthMargin;
            var extraHeight = isActuallyScaled ? 0 : ExtraHeightMargin;

            var bubbleWidth = MaxTextWidth + effectivePadding * 2 + extraWidth;
            var bubbleHeight = (int)(_textHeight + effectivePadding * 2 + extraHeight);

            // Colors
            var bubbleColor = _isGroupChat
                ? Color.FromNonPremultiplied(247, 234, 164, 232)
                : Color.FromNonPremultiplied(255, 255, 255, 232);
            var borderColor = Color.FromNonPremultiplied(0, 0, 0, 200);

            var bubbleRect = new Rectangle((int)_bubblePosition.X, (int)_bubblePosition.Y, bubbleWidth, bubbleHeight);

            // Phase 1: Draw bubble background and borders (no scissor needed)
            _spriteBatch.Begin();

            _spriteBatch.Draw(_whitePixel, bubbleRect, bubbleColor);

            // Draw border (1px on each side)
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.X, bubbleRect.Y, bubbleRect.Width, 1), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.X, bubbleRect.Bottom - 1, bubbleRect.Width, 1), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.X, bubbleRect.Y, 1, bubbleRect.Height), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(bubbleRect.Right - 1, bubbleRect.Y, 1, bubbleRect.Height), borderColor);

            // Draw nub (small triangle pointing down at character)
            var nubX = bubbleRect.X + bubbleRect.Width / 2 - 4;
            var nubY = bubbleRect.Bottom;
            for (int i = 0; i < NubHeight; i++)
            {
                var nubWidth = 8 - i * 2;
                if (nubWidth > 0)
                {
                    _spriteBatch.Draw(_whitePixel, new Rectangle(nubX + i, nubY + i, nubWidth, 1), bubbleColor);
                }
            }

            _spriteBatch.End();

            // Phase 2: Draw text with scissor clipping (only in non-scaled mode)
            if (!_clientWindowSizeProvider.IsScaledMode)
            {
                var graphicsDevice = Game.GraphicsDevice;
                var previousScissorRect = graphicsDevice.ScissorRectangle;

                // Set scissor rect to the inner bubble area (excluding border)
                var textClipRect = new Rectangle(
                    (int)_textPosition.X,
                    (int)_textPosition.Y,
                    bubbleWidth - Padding * 2,
                    bubbleHeight - Padding * 2);
                graphicsDevice.ScissorRectangle = textClipRect;

                _spriteBatch.Begin(rasterizerState: _scissorRasterizerState);

                var lineHeight = font.LineHeight;
                for (int i = 0; i < _wrappedLines.Count; i++)
                {
                    var linePos = new Vector2(_textPosition.X, _textPosition.Y + i * lineHeight);
                    _spriteBatch.DrawString(font, _wrappedLines[i], linePos, Color.Black);
                }

                _spriteBatch.End();

                // Restore previous scissor rect
                graphicsDevice.ScissorRectangle = previousScissorRect;
            }
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible || !_clientWindowSizeProvider.IsScaledMode || _wrappedLines.Count == 0)
                return;

            var font = _contentProvider.Fonts[Constants.FontSize10];

            // Scale text position to screen coordinates
            var screenX = _textPosition.X * scaleFactor + renderOffset.X;
            var screenY = _textPosition.Y * scaleFactor + renderOffset.Y;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            var lineHeight = font.LineHeight;
            for (int i = 0; i < _wrappedLines.Count; i++)
            {
                var linePos = new Vector2(screenX, screenY + i * lineHeight);
                spriteBatch.DrawString(font, _wrappedLines[i], linePos, Color.Black);
            }

            spriteBatch.End();
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
