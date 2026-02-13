using System;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class CodeDrawnItemTransferDialog : XNADialog, IPostScaleDrawable
    {
        public enum TransferType
        {
            DropItems,
            JunkItems,
            GiveItems,
            TradeItems,
            ShopTransfer,
            BankTransfer
        }

        private readonly IUIStyleProvider _styleProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IChatTextBoxActions _chatTextBoxActions;

        private readonly string _prompt;
        private readonly int _totalAmount;

        private CodeDrawnButton _okButton;
        private CodeDrawnButton _cancelButton;
        private IXNATextBox _amount;
        private IXNAButton _slider;

        private BitmapFont _font;

        private bool _sliderDragging;

        private int DialogWidth { get; } = 265;
        private int DialogHeight { get; } = 170;

        public int PostScaleDrawOrder => 200;
        public bool SkipRenderTargetDraw => true;

        public int SelectedAmount => int.Parse(_amount.Text);

        public CodeDrawnItemTransferDialog(IUIStyleProvider styleProvider,
                                           IGameStateProvider gameStateProvider,
                                           IClientWindowSizeProvider clientWindowSizeProvider,
                                           IGraphicsDeviceProvider graphicsDeviceProvider,
                                           IContentProvider contentProvider,
                                           IChatTextBoxActions chatTextBoxActions,
                                           ILocalizedStringFinder localizedStringFinder,
                                           string itemName,
                                           TransferType transferType,
                                           int totalAmount,
                                           EOResourceID message)
        {
            if (!IsValidMessage(message))
                throw new ArgumentOutOfRangeException(nameof(message), "Use one of the approved messages.");

            _styleProvider = styleProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
            _chatTextBoxActions = chatTextBoxActions;
            _totalAmount = totalAmount;

            _font = contentProvider.Fonts[Constants.FontSize09];

            _prompt = $"{localizedStringFinder.GetString(EOResourceID.DIALOG_TRANSFER_HOW_MUCH)} {itemName} {localizedStringFinder.GetString(message)}";

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // Text input for amount
            _amount = new ClearableTextBox(
                new Rectangle(160, 95, 77, 19),
                Constants.FontSize08,
                caretTexture: contentProvider.Textures[ContentProvider.Cursor])
            {
                MaxChars = 8,
                LeftPadding = 4,
                TextColor = _styleProvider.TextPrimary,
                Text = "1",
                Selected = true,
                MaxWidth = 73
            };
            _amount.SetParentControl(this);
            _amount.OnTextChanged += AmountTextChanged;

            // Buttons
            var buttonWidth = 72;
            var buttonHeight = 28;
            var buttonY = DialogHeight - buttonHeight - 12;
            var spacing = 16;

            _okButton = new CodeDrawnButton(_styleProvider, _font, _contentProvider, _clientWindowSizeProvider)
            {
                Text = "OK",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - spacing / 2, buttonY, buttonWidth, buttonHeight)
            };
            _okButton.OnClick += (_, _) => Close(XNADialogResult.OK);
            _okButton.SetParentControl(this);

            _cancelButton = new CodeDrawnButton(_styleProvider, _font, _contentProvider, _clientWindowSizeProvider)
            {
                Text = "Cancel",
                DrawArea = new Rectangle(DialogWidth / 2 + spacing / 2, buttonY, buttonWidth, buttonHeight)
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);

            // Slider button (invisible control for drag input, drawn in post-scale)
            _slider = new XNAButton(
                new Texture2D(Game.GraphicsDevice, 1, 1),
                new Vector2(25, 96),
                new Rectangle(0, 0, 16, 15),
                new Rectangle(0, 0, 16, 15));
            _slider.SetParentControl(this);
            _slider.OnClickDrag += SliderClickDrag;

            DialogClosed += (_, _) => chatTextBoxActions.FocusChatTextBox();

            CenterInGameView();
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            _amount?.Initialize();
            _okButton?.Initialize();
            _cancelButton?.Initialize();
            _slider?.Initialize();

            if (_amount != null)
                _amount.Selected = true;

            base.Initialize();
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            // All visual rendering is done in DrawPostScale.
            // Child controls still handle input in the render target.
            base.OnDrawControl(gameTime);
        }

        public void DrawPostScale(SpriteBatch spriteBatch, float scale, Point renderOffset)
        {
            var scaleFactor = _clientWindowSizeProvider.ScaleFactor;
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            var logicalX = DrawAreaWithParentOffset.X;
            var logicalY = DrawAreaWithParentOffset.Y;
            var scaledPos = new Vector2(
                logicalX * scaleFactor + renderOffset.X,
                logicalY * scaleFactor + renderOffset.Y);

            BitmapFont font = FontScaleHelper.GetScaledFont(_contentProvider, scaleFactor);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            var scaledWidth = (int)(DialogWidth * scaleFactor);
            var scaledHeight = (int)(DialogHeight * scaleFactor);
            var bounds = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, scaledWidth, scaledHeight);

            // Full opaque background
            DrawingPrimitives.DrawRoundedRect(spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);

            // Title bar fill
            var titleBarBounds = new Rectangle(
                (int)scaledPos.X + (int)(borderThickness * scaleFactor),
                (int)scaledPos.Y + (int)(borderThickness * scaleFactor),
                scaledWidth - (int)(borderThickness * 2 * scaleFactor),
                (int)((titleBarHeight - borderThickness) * scaleFactor));
            DrawingPrimitives.DrawFilledRect(spriteBatch, titleBarBounds, _styleProvider.TitleBarBackground);

            // Border
            DrawingPrimitives.DrawRoundedRectBorder(spriteBatch, bounds, _styleProvider.PanelBorder, cornerRadius, borderThickness);

            // Title text
            var titlePos = new Vector2(scaledPos.X + 16 * scaleFactor, scaledPos.Y + 8 * scaleFactor);
            spriteBatch.DrawString(font, "Transfer", titlePos, _styleProvider.TitleBarText);

            // Prompt text
            if (!string.IsNullOrEmpty(_prompt))
            {
                var promptX = scaledPos.X + 16 * scaleFactor;
                var promptY = scaledPos.Y + (titleBarHeight + 8) * scaleFactor;
                var maxWidth = (DialogWidth - 40) * scaleFactor;
                DrawWrappedText(spriteBatch, font, _prompt, promptX, promptY, maxWidth, _styleProvider.TextPrimary);
            }

            // Slider track
            var trackX = (int)(scaledPos.X + 25 * scaleFactor);
            var trackY = (int)(scaledPos.Y + 100 * scaleFactor);
            var trackW = (int)(122 * scaleFactor);
            var trackH = (int)(4 * scaleFactor);
            var trackBounds = new Rectangle(trackX, trackY, trackW, trackH);
            DrawingPrimitives.DrawFilledRect(spriteBatch, trackBounds, new Color(60, 60, 60));
            DrawingPrimitives.DrawRectBorder(spriteBatch, trackBounds, _styleProvider.PanelBorder, 1);

            // Slider handle
            var sliderX = (int)(scaledPos.X + _slider.DrawPosition.X * scaleFactor);
            var sliderY = (int)(scaledPos.Y + 93 * scaleFactor);
            var sliderW = (int)(16 * scaleFactor);
            var sliderH = (int)(15 * scaleFactor);
            var sliderBounds = new Rectangle(sliderX, sliderY, sliderW, sliderH);
            var handleColor = _slider.MouseOver ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(spriteBatch, sliderBounds, handleColor);
            DrawingPrimitives.DrawRectBorder(spriteBatch, sliderBounds, _styleProvider.PanelBorder, 1);

            // Input field background and text
            var inputX = (int)(scaledPos.X + 160 * scaleFactor);
            var inputY = (int)(scaledPos.Y + 95 * scaleFactor);
            var inputW = (int)(77 * scaleFactor);
            var inputH = (int)(19 * scaleFactor);
            var inputBounds = new Rectangle(inputX, inputY, inputW, inputH);
            DrawingPrimitives.DrawFilledRect(spriteBatch, inputBounds, new Color(20, 20, 20));
            DrawingPrimitives.DrawRectBorder(spriteBatch, inputBounds, _styleProvider.PanelBorder, 1);

            // Draw the text from the input box
            var inputText = _amount?.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(inputText))
            {
                var textPos = new Vector2(inputX + 4 * scaleFactor, inputY + 2 * scaleFactor);
                spriteBatch.DrawString(font, inputText, textPos, _styleProvider.TextPrimary);
            }

            // Buttons
            var buttonWidth = (int)(72 * scaleFactor);
            var buttonHeight = (int)(28 * scaleFactor);
            var buttonY = (int)(scaledPos.Y + (DialogHeight - 28 - 12) * scaleFactor);
            var spacing = (int)(16 * scaleFactor);

            var okX = (int)(scaledPos.X + (DialogWidth / 2 - 72 - 8) * scaleFactor);
            var cancelX = (int)(scaledPos.X + (DialogWidth / 2 + 8) * scaleFactor);
            DrawButtonPostScale(spriteBatch, "OK", okX, buttonY, buttonWidth, buttonHeight, font, _okButton.MouseOver);
            DrawButtonPostScale(spriteBatch, "Cancel", cancelX, buttonY, buttonWidth, buttonHeight, font, _cancelButton.MouseOver);

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

        private void SliderClickDrag(object sender, MouseEventArgs e)
        {
            _sliderDragging = true;

            var sliderArea = new Rectangle(25, 96, 122 - _slider.DrawArea.Width, 15);
            var newX = e.DistanceMoved.X + (int)_slider.DrawPosition.X;

            if (newX < sliderArea.X)
                newX = sliderArea.X;
            else if (newX > sliderArea.Width + sliderArea.X)
                newX = sliderArea.Width + sliderArea.X;

            _slider.DrawPosition = new Vector2(newX, _slider.DrawPosition.Y);

            var ratio = (newX - sliderArea.X) / (float)sliderArea.Width;
            _amount.Text = ((int)Math.Round(ratio * _totalAmount) + 1).ToString();

            _sliderDragging = false;
        }

        private void AmountTextChanged(object sender, EventArgs e)
        {
            int amt = 0;
            if (_amount.Text != "" && (!int.TryParse(_amount.Text, out amt) || amt > _totalAmount))
            {
                amt = _totalAmount;
                _amount.Text = $"{_totalAmount}";
            }
            else if (_amount.Text != "" && amt < 0)
            {
                amt = 1;
                _amount.Text = $"{amt}";
            }

            if (!_sliderDragging)
            {
                if (amt <= 1)
                {
                    _slider.DrawPosition = new Vector2(25, 96);
                }
                else
                {
                    int xCoord = (int)Math.Round((amt / (double)_totalAmount) * (122 - _slider.DrawArea.Width));
                    _slider.DrawPosition = new Vector2(25 + xCoord, 96);
                }
            }
        }

        private static bool IsValidMessage(EOResourceID msg)
        {
            var name = Enum.GetName(typeof(EOResourceID), msg);
            return name.Contains("DIALOG_TRANSFER");
        }
    }
}
