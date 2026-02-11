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
    /// A fully code-drawn character creation dialog replacing the broken texture-based version.
    /// Features text-labeled selectors for gender, hair style, hair color, and race,
    /// a name input field, character preview, and OK/Cancel buttons.
    /// </summary>
    public class CodeDrawnCreateCharacterDialog : CodeDrawnDialog
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IXnaControlSoundMapper _xnaControlSoundMapper;
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

        public string Name => _inputBox.Text.Trim();

        private CharacterRenderProperties RenderProperties => _characterControl.RenderProperties;
        public int Gender => RenderProperties.Gender;
        public int HairStyle => RenderProperties.HairStyle;
        public int HairColor => RenderProperties.HairColor;
        public int Race => RenderProperties.Race;

        public CodeDrawnCreateCharacterDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            ICharacterRendererFactory rendererFactory,
            IContentProvider contentProvider,
            IEOMessageBoxFactory messageBoxFactory,
            IXnaControlSoundMapper xnaControlSoundMapper,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider)
            : base(styleProvider, gameStateProvider, clientWindowSizeProvider, graphicsDeviceProvider)
        {
            _styleProvider = styleProvider;
            _messageBoxFactory = messageBoxFactory;
            _xnaControlSoundMapper = xnaControlSoundMapper;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize09];

            DialogWidth = 340;
            DialogHeight = 240;

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // --- Name input ---
            var cursorTexture = contentProvider.Textures[ContentProvider.Cursor];
            _inputBox = new ClearableTextBox(new Rectangle(80, 38, 140, 19), Constants.FontSize08, caretTexture: cursorTexture)
            {
                LeftPadding = 5,
                DefaultText = " ",
                Text = " ",
                MaxChars = 12,
                Selected = true,
                TextColor = ColorConstants.LightBeigeText,
                Visible = true
            };
            _inputBox.SetParentControl(this);
            _inputBox.Selected = true;

            // --- Character preview ---
            _characterControl = new CreateCharacterControl(rendererFactory)
            {
                DrawPosition = new Vector2(242, 50)
            };
            _characterControl.SetParentControl(this);

            // --- Arrow buttons for each property ---
            const int arrowWidth = 28;
            const int arrowHeight = 22;
            const int arrowX = 210;
            const int startY = 70;
            const int rowSpacing = 30;

            _genderArrow = CreateArrowButton(arrowX, startY, arrowWidth, arrowHeight);
            _genderArrow.OnClick += (_, _) => { _characterControl.NextGender(); };

            _hairStyleArrow = CreateArrowButton(arrowX, startY + rowSpacing, arrowWidth, arrowHeight);
            _hairStyleArrow.OnClick += (_, _) =>
            {
                _characterControl.NextHairStyle();
                if (RenderProperties.HairStyle == 0) // skip bald
                    _characterControl.NextHairStyle();
            };

            _hairColorArrow = CreateArrowButton(arrowX, startY + rowSpacing * 2, arrowWidth, arrowHeight);
            _hairColorArrow.OnClick += (_, _) => { _characterControl.NextHairColor(); };

            _raceArrow = CreateArrowButton(arrowX, startY + rowSpacing * 3, arrowWidth, arrowHeight);
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

            // CenterInGameView() uses BackgroundTexture bounds which is null for code-drawn dialogs.
            // Manually center using DrawArea dimensions instead.
            CenterInGameView();
            DrawPosition = new Vector2(
                DrawPosition.X - DialogWidth / 2,
                DrawPosition.Y - DialogHeight / 2);
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

        protected override void OnDrawControl(GameTime gameTime)
        {
            DrawDialogBackground();
            DrawLabelsAndValues();

            base.OnDrawControl(gameTime);
        }

        /// <summary>
        /// Draws the dialog background: rounded rect with title bar.
        /// </summary>
        private void DrawDialogBackground()
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

            // Title bar fill
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle(borderThickness, borderThickness, DrawArea.Width - borderThickness * 2, titleBarHeight - borderThickness),
                _styleProvider.TitleBarBackground);

            // Border
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bounds, _styleProvider.PanelBorder, cornerRadius, borderThickness);

            _spriteBatch.End();
        }

        /// <summary>
        /// Draws the text labels (Name, Gender, Hair Style, Hair Color, Race) and current values.
        /// </summary>
        private void DrawLabelsAndValues()
        {
            var drawPos = DrawAreaWithParentOffset;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);

            // Title
            var titlePos = new Vector2(drawPos.X + 16, drawPos.Y + 8);
            _spriteBatch.DrawString(_labelFont, "Create Character", titlePos, _styleProvider.TitleBarText);

            // Label color - use a bright white for visibility
            var labelColor = Color.White;

            // "Name:" label
            var nameY = drawPos.Y + 40;
            DrawBoldString("Name:", new Vector2(drawPos.X + 20, nameY), labelColor);

            // Input box background
            var inputRect = new Rectangle(drawPos.X + 80, (int)nameY - 2, 142, 21);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, inputRect, new Color(20, 20, 20, 180));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, inputRect, _styleProvider.PanelBorder, 1);

            // Property rows
            const int startY = 70;
            const int rowSpacing = 30;
            const int labelX = 20;

            DrawPropertyRow(drawPos, labelX, startY, "Gender:", GetGenderName());
            DrawPropertyRow(drawPos, labelX, startY + rowSpacing, "Hair Style:", GetHairStyleName());
            DrawPropertyRow(drawPos, labelX, startY + rowSpacing * 2, "Hair Color:", GetHairColorName());
            DrawPropertyRow(drawPos, labelX, startY + rowSpacing * 3, "Race:", GetRaceName());

            _spriteBatch.End();
        }

        private void DrawPropertyRow(Rectangle drawPos, int labelX, int y, string label, string value)
        {
            var rowY = drawPos.Y + y;
            DrawBoldString(label, new Vector2(drawPos.X + labelX, rowY + 3), Color.White);

            // Value background
            var valueRect = new Rectangle(drawPos.X + 100, (int)rowY, 106, 22);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, valueRect, new Color(30, 30, 30, 160));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, valueRect, _styleProvider.PanelBorder, 1);

            DrawBoldString(value, new Vector2(drawPos.X + 106, rowY + 3), _styleProvider.TextHighlight);
        }

        /// <summary>
        /// Draws text with a dark shadow and double-strike for a bolder, more readable appearance.
        /// </summary>
        private void DrawBoldString(string text, Vector2 position, Color color)
        {
            // Draw fully opaque black shadow for depth
            _spriteBatch.DrawString(_labelFont, text, position + new Vector2(1, 1), Color.Black);
            // Double-strike: draw twice at same position to boost alpha of antialiased pixels
            _spriteBatch.DrawString(_labelFont, text, position, color);
            _spriteBatch.DrawString(_labelFont, text, position, color);
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
