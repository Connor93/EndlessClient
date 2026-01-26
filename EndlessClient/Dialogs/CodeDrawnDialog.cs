using System;
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
        private BitmapFont _scaledFont;

        protected int DialogWidth { get; set; } = 290;
        protected int DialogHeight { get; set; } = 120;

        public string Message { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;

        private bool IsScaledMode => _clientWindowSizeProvider != null && Game.Window.AllowUserResizing;

        public int PostScaleDrawOrder => 100;
        public bool SkipRenderTargetDraw => IsScaledMode;

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

        public void SetupDialog(EODialogButtons buttons, BitmapFont font, BitmapFont scaledFont = null)
        {
            _font = font;
            _scaledFont = scaledFont ?? font;
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

            // In scaled mode, hide the child labels to prevent them from drawing in the render target
            if (IsScaledMode)
            {
                _captionLabel?.SetControlUnparented();
                _messageLabel?.SetControlUnparented();
            }

            CenterInGameView();
        }

        private CodeDrawnButton CreateButton(string text, Vector2 position, int width, int height)
        {
            var button = new CodeDrawnButton(_styleProvider, _font, _scaledFont, _clientWindowSizeProvider)
            {
                Text = text,
                DrawArea = new Rectangle((int)position.X, (int)position.Y, width, height)
            };
            button.SetParentControl(this);
            return button;
        }

        public override void CenterInGameView()
        {
            base.CenterInGameView();

            if (_isInGame() && !Game.Window.AllowUserResizing)
                DrawPosition = new Vector2(DrawPosition.X, (330 - DrawArea.Height) / 2f);
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
            if (IsScaledMode)
            {
                DrawFills();
            }
            else
            {
                DrawComplete();
            }

            // Let child buttons draw their fills
            base.OnDrawControl(gameTime);
        }

        /// <summary>
        /// Draws only fills for the render target phase in scaled mode
        /// </summary>
        private void DrawFills()
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main panel background (no border, that's drawn post-scale)
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);

            // Title bar fill
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle(borderThickness, borderThickness, DrawArea.Width - borderThickness * 2, titleBarHeight - borderThickness),
                _styleProvider.TitleBarBackground);

            _spriteBatch.End();
        }

        /// <summary>
        /// Complete drawing for non-scaled mode
        /// </summary>
        private void DrawComplete()
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main panel background
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bounds, _styleProvider.PanelBorder, cornerRadius, borderThickness);

            // Title bar
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle(borderThickness, borderThickness, DrawArea.Width - borderThickness * 2, titleBarHeight - borderThickness),
                _styleProvider.TitleBarBackground);

            _spriteBatch.End();
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scale, Point renderOffset)
        {
            if (!IsScaledMode) return;

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

            // Choose font based on scale factor
            BitmapFont font;
            if (scaleFactor < 1.25f)
                font = _font;
            else if (scaleFactor < 1.75f)
                font = _scaledFont ?? _font;
            else
                font = _scaledFont ?? _font;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Scaled dialog dimensions
            var scaledWidth = (int)(DialogWidth * scaleFactor);
            var scaledHeight = (int)(DialogHeight * scaleFactor);
            var bounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

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
            DrawingPrimitives.DrawRectBorder(spriteBatch, buttonRect, Color.Black, 1);

            var textSize = font.MeasureString(text);
            var textPos = new Vector2(
                x + (width - textSize.Width) / 2,
                y + (height - textSize.Height) / 2);
            spriteBatch.DrawString(font, text, textPos, Color.White);
        }

        private void DrawWrappedText(SpriteBatch spriteBatch, BitmapFont font, string text, float x, float y, float maxWidth, Color color)
        {
            var words = text.Split(' ');
            var line = "";
            var currentY = y;
            var lineHeight = font.LineHeight;

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
        }
    }
}
