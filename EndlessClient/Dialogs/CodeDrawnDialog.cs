using System;
using EndlessClient.Content;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A procedurally-drawn dialog that replaces texture-based dialogs when UIMode=Code.
    /// Features rounded corners, title bar with close button, and styled buttons.
    /// Implements IPostScaleDrawable for crisp text rendering at scale.
    /// </summary>
    public class CodeDrawnDialog : XNADialog, IPostScaleDrawable
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly Func<bool> _isInGame;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private CodeDrawnButton _okButton;
        private CodeDrawnButton _cancelButton;
        private IXNALabel _messageLabel;
        private IXNALabel _captionLabel;
        private BitmapFont _font;
        private IContentProvider _contentProvider;

        protected int DialogWidth { get; set; } = 290;
        protected int DialogHeight { get; set; } = 120;

        public string Message { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;

        public int PostScaleDrawOrder => 300; // Message boxes render above all other dialogs
        public bool SkipRenderTargetDraw => true;

        /// <summary>
        /// Backwards-compatible constructor for non-scaled mode.
        /// </summary>
        public CodeDrawnDialog(IUIStyleProvider styleProvider,
                               IGameStateProvider gameStateProvider)
            : this(styleProvider, gameStateProvider, null, null)
        {
        }

        /// <summary>
        /// Full constructor with post-scale rendering support.
        /// </summary>
        public CodeDrawnDialog(IUIStyleProvider styleProvider,
                               IGameStateProvider gameStateProvider,
                               IClientWindowSizeProvider clientWindowSizeProvider,
                               IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _styleProvider = styleProvider;
            _isInGame = () => gameStateProvider.CurrentState == GameStates.PlayingTheGame;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        public void SetupDialog(EODialogButtons buttons, BitmapFont font, BitmapFont scaledFont = null, IContentProvider contentProvider = null)
        {
            _font = font;
            _contentProvider = contentProvider;

            // Auto-size dialog based on message content
            AutoSizeDialog(font);

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // Title/Caption label
            _captionLabel = new XNALabel(Constants.FontSize10)
            {
                AutoSize = true,
                ForeColor = _styleProvider.TitleBarText,
                Text = Caption,
                TextWidth = DialogWidth - 40,
                DrawPosition = new Vector2(16, 8)
            };
            _captionLabel.SetParentControl(this);

            // Message label
            _messageLabel = new XNALabel(Constants.FontSize10)
            {
                AutoSize = true,
                ForeColor = _styleProvider.TextPrimary,
                Text = Message,
                TextWidth = DialogWidth - 40,
                DrawPosition = new Vector2(16, _styleProvider.TitleBarHeight + 12),
                WrapBehavior = WrapBehavior.WrapToNewLine,
            };
            _messageLabel.SetParentControl(this);

            // Buttons
            var buttonWidth = 72;
            var buttonHeight = 28;
            var buttonY = DialogHeight - buttonHeight - 12;

            switch (buttons)
            {
                case EODialogButtons.Ok:
                    _okButton = CreateButton("OK", new Vector2((DialogWidth - buttonWidth) / 2, buttonY), buttonWidth, buttonHeight);
                    _okButton.OnClick += (_, _) => Close(XNADialogResult.OK);
                    break;
                case EODialogButtons.Cancel:
                    _cancelButton = CreateButton("Cancel", new Vector2((DialogWidth - buttonWidth) / 2, buttonY), buttonWidth, buttonHeight);
                    _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
                    break;
                case EODialogButtons.OkCancel:
                    var spacing = 16;
                    _okButton = CreateButton("OK", new Vector2(DialogWidth / 2 - buttonWidth - spacing / 2, buttonY), buttonWidth, buttonHeight);
                    _okButton.OnClick += (_, _) => Close(XNADialogResult.OK);

                    _cancelButton = CreateButton("Cancel", new Vector2(DialogWidth / 2 + spacing / 2, buttonY), buttonWidth, buttonHeight);
                    _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
                    break;
            }

            // Hide the child labels to prevent them from drawing independently.
            // All rendering is handled in DrawPostScale for correct z-ordering.
            _captionLabel?.SetControlUnparented();
            _messageLabel?.SetControlUnparented();
            // Suppress button post-scale drawing (dialog draws them manually)
            // but keep them parented for input handling.
            if (_okButton != null) _okButton.SuppressPostScaleDraw = true;
            if (_cancelButton != null) _cancelButton.SuppressPostScaleDraw = true;

            CenterInGameView();
        }

        private void AutoSizeDialog(BitmapFont font)
        {
            if (string.IsNullOrEmpty(Message))
                return;

            var titleBarHeight = _styleProvider.TitleBarHeight;
            var lineHeight = font.LineHeight;
            var padding = 40; // horizontal padding (16 each side + margin)
            var buttonAreaHeight = 48; // button height + bottom margin

            // Split by explicit newlines first
            var explicitLines = Message.Split('\n');
            var totalLines = 0;
            var maxLineWidth = 0f;

            foreach (var explicitLine in explicitLines)
            {
                var lineWidth = font.MeasureString(explicitLine).Width;
                if (lineWidth > maxLineWidth)
                    maxLineWidth = lineWidth;

                // Count wrapped lines within each explicit line
                var availableWidth = DialogWidth - padding;
                if (lineWidth <= availableWidth)
                {
                    totalLines++;
                }
                else
                {
                    // Estimate line wrapping
                    totalLines += Math.Max(1, (int)Math.Ceiling(lineWidth / availableWidth));
                }
            }

            // Widen if needed (cap at 360)
            if (maxLineWidth + padding > DialogWidth)
                DialogWidth = Math.Min(360, (int)(maxLineWidth + padding));

            // Calculate required height
            var contentHeight = titleBarHeight + 12 + (totalLines * lineHeight) + buttonAreaHeight;
            if (contentHeight > DialogHeight)
                DialogHeight = contentHeight;
        }

        private CodeDrawnButton CreateButton(string text, Vector2 position, int width, int height)
        {
            var button = new CodeDrawnButton(_styleProvider, _font, _contentProvider, _clientWindowSizeProvider)
            {
                Text = text,
                DrawArea = new Rectangle((int)position.X, (int)position.Y, width, height)
            };
            button.SetParentControl(this);
            return button;
        }

        public override void CenterInGameView()
        {
            int centerWidth, centerHeight;
            if (_clientWindowSizeProvider != null)
            {
                centerWidth = _clientWindowSizeProvider.GameWidth;
                centerHeight = _clientWindowSizeProvider.GameHeight;
            }
            else if (Game?.GraphicsDevice != null)
            {
                var viewport = Game.GraphicsDevice.Viewport;
                centerWidth = viewport.Width;
                centerHeight = viewport.Height;
            }
            else return;

            DrawPosition = new Vector2(centerWidth / 2 - DialogWidth / 2,
                                       centerHeight / 2 - DialogHeight / 2);
        }

        public override void Initialize()
        {
            if (_graphicsDeviceProvider != null)
                DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            else
                DrawingPrimitives.Initialize(Game.GraphicsDevice);

            _captionLabel?.Initialize();
            _messageLabel?.Initialize();
            _okButton?.Initialize();
            _cancelButton?.Initialize();

            base.Initialize();
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            // All drawing (background, buttons, text) is done in DrawPostScale
            // for correct z-ordering. Don't draw children here.
        }


        public void DrawPostScale(SpriteBatch spriteBatch, float scale, Point renderOffset)
        {

            var scaleFactor = _clientWindowSizeProvider.ScaleFactor;
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            // Calculate scaled position (include renderOffset for letterboxing/pillarboxing)
            var logicalX = DrawAreaWithParentOffset.X;
            var logicalY = DrawAreaWithParentOffset.Y;
            var scaledPos = new Vector2(
                logicalX * scaleFactor + renderOffset.X,
                logicalY * scaleFactor + renderOffset.Y);

            // Choose font for button text - use FontSize10 to match Close button style
            var font = _contentProvider != null
                ? _contentProvider.Fonts[Constants.FontSize10]
                : _font;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Scaled dialog dimensions
            var scaledWidth = (int)(DialogWidth * scaleFactor);
            var scaledHeight = (int)(DialogHeight * scaleFactor);
            var bounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            // Draw background fills at post-scale so dialog appears above other dialogs
            DrawingPrimitives.DrawRoundedRect(spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);
            DrawingPrimitives.DrawFilledRect(spriteBatch,
                new Rectangle((int)scaledPos.X + (int)(borderThickness * scaleFactor),
                              (int)scaledPos.Y + (int)(borderThickness * scaleFactor),
                              scaledWidth - (int)(borderThickness * 2 * scaleFactor),
                              (int)((titleBarHeight - borderThickness) * scaleFactor)),
                _styleProvider.TitleBarBackground);

            // Border
            DrawingPrimitives.DrawRoundedRectBorder(spriteBatch, bounds, _styleProvider.PanelBorder, cornerRadius, borderThickness);

            // Caption text
            if (!string.IsNullOrEmpty(Caption))
            {
                var captionPos = new Vector2(scaledPos.X + 16 * scaleFactor, scaledPos.Y + 8 * scaleFactor);
                spriteBatch.DrawString(font, Caption, captionPos, _styleProvider.TitleBarText);
            }

            // Message text (with word wrapping approximation)
            if (!string.IsNullOrEmpty(Message))
            {
                var messageX = scaledPos.X + 16 * scaleFactor;
                var messageY = scaledPos.Y + (titleBarHeight + 12) * scaleFactor;
                var maxWidth = (DialogWidth - 40) * scaleFactor;

                DrawWrappedText(spriteBatch, font, Message, messageX, messageY, maxWidth, _styleProvider.TextPrimary);
            }

            // Button drawing
            var buttonWidth = (int)(72 * scaleFactor);
            var buttonHeight = (int)(28 * scaleFactor);
            var buttonY = (int)(scaledPos.Y + (DialogHeight - 28 - 12) * scaleFactor);

            if (_okButton != null && _cancelButton != null)
            {
                // Two buttons
                var spacing = (int)(16 * scaleFactor);
                var okX = (int)(scaledPos.X + (DialogWidth / 2 - 72 - 8) * scaleFactor);
                var cancelX = (int)(scaledPos.X + (DialogWidth / 2 + 8) * scaleFactor);
                DrawButtonPostScale(spriteBatch, "OK", okX, buttonY, buttonWidth, buttonHeight, font, _okButton.MouseOver);
                DrawButtonPostScale(spriteBatch, "Cancel", cancelX, buttonY, buttonWidth, buttonHeight, font, _cancelButton.MouseOver);
            }
            else if (_okButton != null)
            {
                var okX = (int)(scaledPos.X + (DialogWidth - 72) / 2 * scaleFactor);
                DrawButtonPostScale(spriteBatch, "OK", okX, buttonY, buttonWidth, buttonHeight, font, _okButton.MouseOver);
            }
            else if (_cancelButton != null)
            {
                var cancelX = (int)(scaledPos.X + (DialogWidth - 72) / 2 * scaleFactor);
                DrawButtonPostScale(spriteBatch, "Cancel", cancelX, buttonY, buttonWidth, buttonHeight, font, _cancelButton.MouseOver);
            }

            spriteBatch.End();
        }

        private void DrawButtonPostScale(SpriteBatch spriteBatch, string text, int x, int y, int width, int height, BitmapFont font, bool isHovered)
        {
            var buttonRect = new Rectangle(x, y, width, height);
            var buttonColor = isHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(spriteBatch, buttonRect, buttonColor);
            DrawingPrimitives.DrawRectBorder(spriteBatch, buttonRect, _styleProvider.ButtonBorder, 1);

            var textSize = font.MeasureString(text);
            var textPos = new Vector2(
                x + (width - textSize.Width) / 2,
                y + (height - textSize.Height) / 2);
            spriteBatch.DrawString(font, text, textPos, _styleProvider.ButtonText);
        }

        private void DrawWrappedText(SpriteBatch spriteBatch, BitmapFont font, string text, float x, float y, float maxWidth, Color color)
        {
            var currentY = y;
            var lineHeight = font.LineHeight;

            // Split by explicit newlines first, then word-wrap each segment
            var paragraphs = text.Split('\n');
            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrEmpty(paragraph))
                {
                    currentY += lineHeight;
                    continue;
                }

                var words = paragraph.Split(' ');
                var line = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    var testSize = font.MeasureString(testLine);

                    if (testSize.Width > maxWidth && !string.IsNullOrEmpty(line))
                    {
                        spriteBatch.DrawString(font, line, new Vector2(x, currentY), color);
                        currentY += lineHeight;
                        line = word;
                    }
                    else
                    {
                        line = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    spriteBatch.DrawString(font, line, new Vector2(x, currentY), color);
                }

                currentY += lineHeight;
            }
        }
    }
}
