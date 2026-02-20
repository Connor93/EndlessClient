using System;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EndlessClient.Rendering.Factories;
using EOLib.Domain.Character;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A fully code-drawn character creation dialog.
    /// Inherits from XNADialog directly and implements IPostScaleDrawable
    /// so that fills and the character preview draw to the render target
    /// while crisp text/borders are drawn post-scale.
    /// </summary>
    public class CodeDrawnCreateCharacterDialog : XNADialog, IPostScaleDrawable
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IXnaControlSoundMapper _xnaControlSoundMapper;
        private readonly IContentProvider _contentProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;

        private readonly IXNATextBox _inputBox;
        private readonly CreateCharacterControl _characterControl;

        private readonly CodeDrawnButton _genderArrow;
        private readonly CodeDrawnButton _hairStyleArrow;
        private readonly CodeDrawnButton _hairColorArrow;
        private readonly CodeDrawnButton _raceArrow;

        private readonly CodeDrawnButton _okButton;
        private readonly CodeDrawnButton _cancelButton;

        private static readonly string[] GenderNames = { "Female", "Male" };
        private static readonly string[] HairColorNames = { "Brown", "Green", "Pink", "Red", "Yellow", "Purple", "Blue", "White", "Black", "Grey" };
        private static readonly string[] RaceNames = { "White", "Tan", "Pale", "Orc" };
        private const int MaxRaces = 4; // exclude skeleton and panda skins

        private const int DialogWidth = 340;
        private const int DialogHeight = 240;

        // Layout constants
        private const int LabelX = 20;
        private const int ValueX = 100;
        private const int ValueWidth = 106;
        private const int ValueHeight = 22;
        private const int ArrowX = 210;
        private const int ArrowWidth = 28;
        private const int ArrowHeight = 22;
        private const int StartY = 70;
        private const int RowSpacing = 30;

        public string Name => _inputBox.Text.Trim();

        private CharacterRenderProperties RenderProperties => _characterControl.RenderProperties;
        public int Gender => RenderProperties.Gender;
        public int HairStyle => RenderProperties.HairStyle;
        public int HairColor => RenderProperties.HairColor;
        public int Race => RenderProperties.Race;

        // IPostScaleDrawable implementation
        public int PostScaleDrawOrder => 100;
        public bool SkipRenderTargetDraw => false;

        public CodeDrawnCreateCharacterDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            ICharacterRendererFactory rendererFactory,
            IContentProvider contentProvider,
            IEOMessageBoxFactory messageBoxFactory,
            IXnaControlSoundMapper xnaControlSoundMapper,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _styleProvider = styleProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _messageBoxFactory = messageBoxFactory;
            _xnaControlSoundMapper = xnaControlSoundMapper;
            _contentProvider = contentProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize09];

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // --- Name input ---
            var cursorTexture = contentProvider.Textures[ContentProvider.Cursor];
            _inputBox = new ClearableTextBox(new Rectangle(ValueX, 38, ValueWidth + 40, 19), Constants.FontSize08, caretTexture: cursorTexture)
            {
                LeftPadding = 5,
                DefaultText = " ",
                Text = " ",
                MaxChars = 12,
                Selected = true,
                TextColor = _styleProvider.InputText,
                Visible = true
            };
            _inputBox.SetParentControl(this);
            _inputBox.Selected = true;

            // --- Character preview ---
            _characterControl = new CreateCharacterControl(rendererFactory)
            {
                DrawPosition = new Vector2(260, 50),
                MaxHairStyle = 50,
            };
            _characterControl.SetParentControl(this);

            // --- Arrow buttons for each property ---
            _genderArrow = CreateArrowButton(ArrowX, StartY, ArrowWidth, ArrowHeight);
            _genderArrow.OnClick += (_, _) => { _characterControl.NextGender(); };

            _hairStyleArrow = CreateArrowButton(ArrowX, StartY + RowSpacing, ArrowWidth, ArrowHeight);
            _hairStyleArrow.OnClick += (_, _) =>
            {
                _characterControl.NextHairStyle();
                if (RenderProperties.HairStyle == 0) // skip bald
                    _characterControl.NextHairStyle();
            };

            _hairColorArrow = CreateArrowButton(ArrowX, StartY + RowSpacing * 2, ArrowWidth, ArrowHeight);
            _hairColorArrow.OnClick += (_, _) => { _characterControl.NextHairColor(); };

            _raceArrow = CreateArrowButton(ArrowX, StartY + RowSpacing * 3, ArrowWidth, ArrowHeight);
            _raceArrow.OnClick += (_, _) => { NextRaceConstrained(); };

            // --- OK / Cancel buttons ---
            const int buttonWidth = 72;
            const int buttonHeight = 28;
            var buttonY = DialogHeight - buttonHeight - 14;

            _okButton = new CodeDrawnButton(_styleProvider, _font)
            {
                Text = "OK",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - 8, buttonY, buttonWidth, buttonHeight)
            };
            _okButton.OnClick += (_, _) => ClickOk();
            _okButton.SetParentControl(this);

            _cancelButton = new CodeDrawnButton(_styleProvider, _font)
            {
                Text = "Cancel",
                DrawArea = new Rectangle(DialogWidth / 2 + 8, buttonY, buttonWidth, buttonHeight)
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);

            CenterInGameView();
        }

        public override void CenterInGameView()
        {
            base.CenterInGameView();

            int centerWidth, centerHeight;
            if (XNADialog.GameViewportProvider != null)
            {
                centerWidth = XNADialog.GameViewportProvider.GameWidth;
                centerHeight = XNADialog.GameViewportProvider.GameHeight;
            }
            else
            {
                centerWidth = Game.GraphicsDevice.Viewport.Width;
                centerHeight = Game.GraphicsDevice.Viewport.Height;
            }

            var centerX = (centerWidth - DialogWidth) / 2;
            var centerY = (centerHeight - DialogHeight) / 2;
            DrawPosition = new Vector2(centerX, centerY);
        }

        private CodeDrawnButton CreateArrowButton(int x, int y, int width, int height)
        {
            var btn = new CodeDrawnButton(_styleProvider, _font)
            {
                Text = ">",
                DrawArea = new Rectangle(x, y, width, height)
            };
            btn.SetParentControl(this);
            return btn;
        }

        public override void Initialize()
        {
            if (Game?.GraphicsDevice != null)
                DrawingPrimitives.Initialize(Game.GraphicsDevice);

            _characterControl.Initialize();
            _xnaControlSoundMapper.BindSoundToControl(_characterControl);

            _inputBox.Initialize();

            _genderArrow.Initialize();
            _hairStyleArrow.Initialize();
            _hairColorArrow.Initialize();
            _raceArrow.Initialize();

            _okButton.Initialize();
            _cancelButton.Initialize();

            base.Initialize();
        }

        /// <summary>
        /// Draws fills and the character preview to the render target.
        /// Text and borders are drawn in DrawPostScale for crisp rendering.
        /// </summary>
        protected override void OnDrawControl(GameTime gameTime)
        {
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            _spriteBatch.Begin(transformMatrix: transform);

            var bounds = new Rectangle(0, 0, DialogWidth, DialogHeight);
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            // Main panel background
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);

            // Title bar fill
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle(borderThickness, borderThickness, DialogWidth - borderThickness * 2, titleBarHeight - borderThickness),
                _styleProvider.TitleBarBackground);

            // Name input background
            var inputRect = new Rectangle(ValueX, 36, ValueWidth + 40, 21);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, inputRect, _styleProvider.InputBackground);

            // Value background fields
            for (int i = 0; i < 4; i++)
            {
                var valueRect = new Rectangle(ValueX, StartY + RowSpacing * i, ValueWidth, ValueHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, valueRect, _styleProvider.PanelBackgroundAlt);
            }

            _spriteBatch.End();

            // Draw children (character preview, input box, etc.) in the render target
            base.OnDrawControl(gameTime);
        }

        /// <summary>
        /// Draws crisp text labels and borders at post-scale coordinates.
        /// </summary>
        public void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var drawPos = DrawAreaWithParentOffset;
            var scaledX = (int)(drawPos.X * scaleFactor) + renderOffset.X;
            var scaledY = (int)(drawPos.Y * scaleFactor) + renderOffset.Y;
            var scaledWidth = (int)(DialogWidth * scaleFactor);
            var scaledHeight = (int)(DialogHeight * scaleFactor);

            // Select appropriate font based on scale
            var font = FontScaleHelper.GetScaledFont(_contentProvider, scaleFactor);

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            try
            {
                // Border
                var cornerRadius = (int)Math.Max(_styleProvider.CornerRadius, _styleProvider.CornerRadius * scaleFactor);
                var borderThick = (int)Math.Max(2, _styleProvider.BorderThickness * scaleFactor);
                DrawingPrimitives.DrawRoundedRectBorder(spriteBatch,
                    new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight),
                    _styleProvider.PanelBorder, cornerRadius, borderThick);

                // Title text
                var titlePos = new Vector2((int)(scaledX + 16 * scaleFactor), (int)(scaledY + 8 * scaleFactor));
                spriteBatch.DrawString(font, "Create Character", titlePos, _styleProvider.TitleBarText);

                // Name label
                var nameY = (int)(scaledY + 40 * scaleFactor);
                spriteBatch.DrawString(font, "Name:", new Vector2((int)(scaledX + LabelX * scaleFactor), nameY), _styleProvider.TextPrimary);

                // Name input border
                var inputRect = new Rectangle(
                    (int)(scaledX + ValueX * scaleFactor),
                    (int)(scaledY + 36 * scaleFactor),
                    (int)((ValueWidth + 40) * scaleFactor),
                    (int)(21 * scaleFactor));
                DrawingPrimitives.DrawRectBorder(spriteBatch, inputRect, _styleProvider.InputBorder, 1);

                // Property rows
                DrawScaledPropertyRow(spriteBatch, font, scaledX, scaledY, scaleFactor, StartY, "Gender:", GetGenderName());
                DrawScaledPropertyRow(spriteBatch, font, scaledX, scaledY, scaleFactor, StartY + RowSpacing, "Hair Style:", GetHairStyleName());
                DrawScaledPropertyRow(spriteBatch, font, scaledX, scaledY, scaleFactor, StartY + RowSpacing * 2, "Hair Color:", GetHairColorName());
                DrawScaledPropertyRow(spriteBatch, font, scaledX, scaledY, scaleFactor, StartY + RowSpacing * 3, "Race:", GetRaceName());

                // Value field borders
                for (int i = 0; i < 4; i++)
                {
                    var valueRect = new Rectangle(
                        (int)(scaledX + ValueX * scaleFactor),
                        (int)(scaledY + (StartY + RowSpacing * i) * scaleFactor),
                        (int)(ValueWidth * scaleFactor),
                        (int)(ValueHeight * scaleFactor));
                    DrawingPrimitives.DrawRectBorder(spriteBatch, valueRect, _styleProvider.PanelBorder, 1);
                }
            }
            finally
            {
                spriteBatch.End();
            }
        }

        private void DrawScaledPropertyRow(SpriteBatch spriteBatch, BitmapFont font, int scaledX, int scaledY, float scaleFactor, int y, string label, string value)
        {
            var labelPos = new Vector2((int)(scaledX + LabelX * scaleFactor), (int)(scaledY + (y + 3) * scaleFactor));
            spriteBatch.DrawString(font, label, labelPos, _styleProvider.TextPrimary);

            var valuePos = new Vector2((int)(scaledX + (ValueX + 6) * scaleFactor), (int)(scaledY + (y + 3) * scaleFactor));
            spriteBatch.DrawString(font, value, valuePos, _styleProvider.TextPrimary);
        }

        private string GetGenderName()
        {
            var idx = RenderProperties.Gender;
            return idx >= 0 && idx < GenderNames.Length ? GenderNames[idx] : $"Gender {idx}";
        }

        private string GetHairStyleName()
        {
            return $"Style {RenderProperties.HairStyle}";
        }

        private string GetHairColorName()
        {
            var idx = RenderProperties.HairColor;
            return idx >= 0 && idx < HairColorNames.Length ? HairColorNames[idx] : $"Color {idx}";
        }

        private string GetRaceName()
        {
            var idx = RenderProperties.Race;
            return idx >= 0 && idx < RaceNames.Length ? RaceNames[idx] : $"Race {idx}";
        }

        private void NextRaceConstrained()
        {
            _characterControl.NextRace();
            // If we've gone past the valid human skins, wrap back to 0
            if (RenderProperties.Race >= MaxRaces)
            {
                // Keep cycling until we wrap back to 0
                while (RenderProperties.Race != 0)
                    _characterControl.NextRace();
            }
        }

        private void ClickOk()
        {
            if (_inputBox.Text.Trim().Length < 4)
            {
                var messageBox = _messageBoxFactory.CreateMessageBox(
                    DialogResourceID.CHARACTER_CREATE_NAME_TOO_SHORT,
                    EODialogButtons.Ok,
                    EOMessageBoxStyle.SmallDialogLargeHeader);
                messageBox.ShowDialog();
            }
            else
            {
                Close(XNADialogResult.OK);
            }
        }
    }
}
