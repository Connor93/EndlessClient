using System;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A procedurally-drawn dialog that replaces texture-based dialogs when UIMode=Code.
    /// Features rounded corners, title bar with close button, and styled buttons.
    /// </summary>
    public class CodeDrawnDialog : XNADialog
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly Func<bool> _isInGame;
        private CodeDrawnButton _okButton;
        private CodeDrawnButton _cancelButton;
        private IXNALabel _messageLabel;
        private IXNALabel _captionLabel;
        private BitmapFont _font;

        protected int DialogWidth { get; set; } = 290;
        protected int DialogHeight { get; set; } = 120;

        public string Message { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;

        public CodeDrawnDialog(IUIStyleProvider styleProvider,
                               IGameStateProvider gameStateProvider)
        {
            _styleProvider = styleProvider;
            _isInGame = () => gameStateProvider.CurrentState == GameStates.PlayingTheGame;
        }

        public void SetupDialog(EODialogButtons buttons, BitmapFont font)
        {
            _font = font;
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

            CenterInGameView();
        }

        private CodeDrawnButton CreateButton(string text, Vector2 position, int width, int height)
        {
            var button = new CodeDrawnButton(_styleProvider, _font)
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
            DrawingPrimitives.Initialize(Game.GraphicsDevice);

            _captionLabel?.Initialize();
            _messageLabel?.Initialize();
            _okButton?.Initialize();
            _cancelButton?.Initialize();

            base.Initialize();
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            // Use transformation matrix to offset drawing to dialog's screen position
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main panel background
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bounds, _styleProvider.PanelBorder, cornerRadius, borderThickness);

            // Title bar (top portion with different color)
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(borderThickness, borderThickness, DrawArea.Width - borderThickness * 2, titleBarHeight - borderThickness), _styleProvider.TitleBarBackground);

            _spriteBatch.End();

            // Let child labels and buttons draw
            base.OnDrawControl(gameTime);
        }
    }
}
