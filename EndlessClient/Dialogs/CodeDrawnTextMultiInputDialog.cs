using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Chat;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class CodeDrawnTextMultiInputDialog : XNADialog, IPostScaleDrawable, ITextMultiInputDialog
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly Func<bool> _isInGame;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IContentProvider _contentProvider;
        private readonly IChatTextBoxActions _chatTextBoxActions;

        private readonly string _title;
        private readonly string _prompt;
        private readonly TextMultiInputDialog.InputInfo[] _inputInfo;

        private CodeDrawnButton _okButton;
        private CodeDrawnButton _cancelButton;
        private readonly ClearableTextBox[] _inputBoxes;

        private BitmapFont _font;

        private bool _suppressTextChangedEvent;

        // Layout constants
        private const int PromptHeight = 30;
        private const int InputRowHeight = 46;
        private const int ButtonRowHeight = 40;

        private int DialogWidth { get; } = 290;
        private int DialogHeight { get; set; }

        public int PostScaleDrawOrder => 200;
        public bool SkipRenderTargetDraw => true;

        public IReadOnlyList<string> Responses => _inputBoxes.Select(x => x.Text).ToList();

        public CodeDrawnTextMultiInputDialog(IUIStyleProvider styleProvider,
                                             IGameStateProvider gameStateProvider,
                                             IClientWindowSizeProvider clientWindowSizeProvider,
                                             IGraphicsDeviceProvider graphicsDeviceProvider,
                                             IContentProvider contentProvider,
                                             IChatTextBoxActions chatTextBoxActions,
                                             string title,
                                             string prompt,
                                             TextMultiInputDialog.InputInfo[] inputInfo)
        {
            _styleProvider = styleProvider;
            _isInGame = () => gameStateProvider.CurrentState == GameStates.PlayingTheGame;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _contentProvider = contentProvider;
            _chatTextBoxActions = chatTextBoxActions;
            _title = title;
            _prompt = prompt;
            _inputInfo = inputInfo;

            _font = contentProvider.Fonts[Constants.FontSize09];

            var titleBarHeight = _styleProvider.TitleBarHeight;
            var inputCount = inputInfo.Length;

            DialogHeight = titleBarHeight + PromptHeight + inputCount * InputRowHeight + ButtonRowHeight + 8;

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // Create input boxes (handle keyboard input, visual drawn in PostScale)
            _inputBoxes = new ClearableTextBox[inputCount];
            var currentY = titleBarHeight + PromptHeight;

            for (int i = 0; i < inputCount; i++)
            {
                var info = inputInfo[i];
                var inputBoxY = currentY + 20;

                _inputBoxes[i] = new ClearableTextBox(
                    new Rectangle(20, inputBoxY, DialogWidth - 40, 22),
                    Constants.FontSize08,
                    caretTexture: contentProvider.Textures[ContentProvider.Cursor])
                {
                    MaxChars = info.MaxChars,
                    LeftPadding = 4,
                    TextColor = _styleProvider.TextPrimary,
                    MaxWidth = DialogWidth - 44
                };
                if (!string.IsNullOrEmpty(info.DefaultValue))
                    _inputBoxes[i].Text = info.DefaultValue;

                _inputBoxes[i].SetParentControl(this);

                // Apply restrictions
                if (info.InputRestriction == TextMultiInputDialog.InputInfo.InputRestrict.Uppercase)
                {
                    var box = _inputBoxes[i];
                    box.OnTextChanged += (_, _) =>
                    {
                        if (_suppressTextChangedEvent) return;
                        _suppressTextChangedEvent = true;
                        box.Text = box.Text.ToUpper();
                        _suppressTextChangedEvent = false;
                    };
                }
                else if (info.InputRestriction == TextMultiInputDialog.InputInfo.InputRestrict.Numeric)
                {
                    var box = _inputBoxes[i];
                    box.OnTextChanged += (_, _) =>
                    {
                        if (_suppressTextChangedEvent) return;
                        _suppressTextChangedEvent = true;
                        var filtered = new string(box.Text.Where(char.IsDigit).ToArray());
                        if (filtered != box.Text) box.Text = filtered;
                        _suppressTextChangedEvent = false;
                    };
                }

                currentY += InputRowHeight;
            }

            // Buttons (handle clicks, visual drawn in PostScale)
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
            _okButton.SuppressPostScaleDraw = true;

            _cancelButton = new CodeDrawnButton(_styleProvider, _font, _contentProvider, _clientWindowSizeProvider)
            {
                Text = "Cancel",
                DrawArea = new Rectangle(DialogWidth / 2 + spacing / 2, buttonY, buttonWidth, buttonHeight)
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);
            _cancelButton.SuppressPostScaleDraw = true;

            DialogClosed += (_, _) => chatTextBoxActions.FocusChatTextBox();

            CenterInGameView();
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            foreach (var box in _inputBoxes)
                box?.Initialize();
            _okButton?.Initialize();
            _cancelButton?.Initialize();

            if (_inputBoxes.Length > 0)
                _inputBoxes[0].Selected = true;

            base.Initialize();
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            // All visual rendering is done in DrawPostScale to ensure
            // the dialog renders on top of other PostScale elements (guild panel, chat).
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

            // Full opaque background (covers guild panel and other PostScale elements)
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
            var titleTextPos = new Vector2(scaledPos.X + 16 * scaleFactor, scaledPos.Y + 8 * scaleFactor);
            spriteBatch.DrawString(font, _title, titleTextPos, _styleProvider.TitleBarText);

            // Prompt text
            if (!string.IsNullOrEmpty(_prompt))
            {
                var promptX = scaledPos.X + 16 * scaleFactor;
                var promptY = scaledPos.Y + (titleBarHeight + 8) * scaleFactor;
                var maxWidth = (DialogWidth - 40) * scaleFactor;
                DrawWrappedText(spriteBatch, font, _prompt, promptX, promptY, maxWidth, _styleProvider.TextPrimary);
            }

            // Input fields: labels, backgrounds, and text
            var fieldStartY = titleBarHeight + PromptHeight;

            for (int i = 0; i < _inputInfo.Length; i++)
            {
                // Label
                var labelY = fieldStartY + 2;
                var labelPos = new Vector2(
                    scaledPos.X + 20 * scaleFactor,
                    scaledPos.Y + labelY * scaleFactor);
                spriteBatch.DrawString(font, _inputInfo[i].Label, labelPos, _styleProvider.TextPrimary);

                // Input field background
                var inputBoxY = fieldStartY + 20;
                var inputX = (int)(scaledPos.X + 20 * scaleFactor);
                var inputYScaled = (int)(scaledPos.Y + inputBoxY * scaleFactor);
                var inputW = (int)((DialogWidth - 40) * scaleFactor);
                var inputH = (int)(22 * scaleFactor);
                var inputBounds = new Rectangle(inputX, inputYScaled, inputW, inputH);
                DrawingPrimitives.DrawFilledRect(spriteBatch, inputBounds, _styleProvider.PanelBackgroundAlt);
                DrawingPrimitives.DrawRectBorder(spriteBatch, inputBounds, _styleProvider.PanelBorder, 1);

                // Input text
                var inputText = _inputBoxes[i]?.Text ?? string.Empty;
                if (!string.IsNullOrEmpty(inputText))
                {
                    var textPos = new Vector2(inputX + 4 * scaleFactor, inputYScaled + 3 * scaleFactor);
                    spriteBatch.DrawString(font, inputText, textPos, _styleProvider.TextPrimary);
                }

                fieldStartY += InputRowHeight;
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
    }
}
