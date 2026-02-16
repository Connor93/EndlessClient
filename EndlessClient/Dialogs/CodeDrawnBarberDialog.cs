using System;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Factories;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Barber;
using EOLib.Graphics;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A fully code-drawn barber dialog replacing the texture-based version.
    /// Inherits from XNADialog directly and implements IPostScaleDrawable
    /// so that fills and the character preview draw to the render target
    /// while crisp text/borders are drawn post-scale.
    /// </summary>
    public class CodeDrawnBarberDialog : XNADialog, IPostScaleDrawable
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;

        private readonly CreateCharacterControl _characterControl;
        private readonly ICharacterRepository _characterRepository;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IBarberActions _barberActions;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IContentProvider _contentProvider;

        private readonly CodeDrawnButton _hairStyleNextArrow;
        private readonly CodeDrawnButton _hairStylePrevArrow;
        private readonly CodeDrawnButton _hairColorNextArrow;
        private readonly CodeDrawnButton _hairColorPrevArrow;
        private readonly CodeDrawnButton _buyButton;
        private readonly CodeDrawnButton _cancelButton;

        private static readonly string[] HairColorNames = { "Brown", "Green", "Pink", "Red", "Blonde", "Blue", "Purple", "Luna", "White", "Black" };

        private const int DialogWidth = 400;
        private const int DialogHeight = 220;

        private CharacterRenderProperties RenderProperties => _characterControl.RenderProperties;

        public int PostScaleDrawOrder => 100;
        public bool SkipRenderTargetDraw => false;

        public CodeDrawnBarberDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            ICharacterRendererFactory rendererFactory,
            IContentProvider contentProvider,
            IEOMessageBoxFactory messageBoxFactory,
            ICharacterRepository characterRepository,
            ILocalizedStringFinder localizedStringFinder,
            IBarberActions barberActions,
            ICharacterInventoryProvider characterInventoryProvider,
            IEIFFileProvider eifFileProvider,
            ISfxPlayer sfxPlayer,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _styleProvider = styleProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _messageBoxFactory = messageBoxFactory;
            _characterRepository = characterRepository;
            _localizedStringFinder = localizedStringFinder;
            _barberActions = barberActions;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _sfxPlayer = sfxPlayer;
            _contentProvider = contentProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize09];

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // --- Character preview (initialized from current character appearance) ---
            var mainCharacterRenderProperties = _characterRepository.MainCharacter.RenderProperties;
            _characterControl = new CreateCharacterControl(mainCharacterRenderProperties, rendererFactory)
            {
                DrawPosition = new Vector2(300, 40),
                MaxHairStyle = 50,
            };
            _characterControl.SetParentControl(this);

            // --- Arrow buttons for hair style and color ---
            const int arrowWidth = 28;
            const int arrowHeight = 22;
            const int prevArrowX = 100;
            const int nextArrowX = 240;
            const int startY = 60;
            const int rowSpacing = 30;

            _hairStylePrevArrow = CreateArrowButton(prevArrowX, startY, arrowWidth, arrowHeight, "<");
            _hairStylePrevArrow.OnClick += (_, _) => { _characterControl.PrevHairStyle(); };

            _hairStyleNextArrow = CreateArrowButton(nextArrowX, startY, arrowWidth, arrowHeight, ">");
            _hairStyleNextArrow.OnClick += (_, _) => { _characterControl.NextHairStyle(); };

            _hairColorPrevArrow = CreateArrowButton(prevArrowX, startY + rowSpacing, arrowWidth, arrowHeight, "<");
            _hairColorPrevArrow.OnClick += (_, _) => { _characterControl.PrevHairColor(); };

            _hairColorNextArrow = CreateArrowButton(nextArrowX, startY + rowSpacing, arrowWidth, arrowHeight, ">");
            _hairColorNextArrow.OnClick += (_, _) => { _characterControl.NextHairColor(); };

            // --- Buy / Cancel buttons ---
            const int buttonWidth = 72;
            const int buttonHeight = 28;
            var buttonY = DialogHeight - buttonHeight - 14;

            _buyButton = new CodeDrawnButton(_styleProvider, _font)
            {
                Text = "Buy",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - 8, buttonY, buttonWidth, buttonHeight)
            };
            _buyButton.OnClick += (_, _) => BuyHair();
            _buyButton.SetParentControl(this);

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

        private CodeDrawnButton CreateArrowButton(int x, int y, int width, int height, string text = ">")
        {
            var btn = new CodeDrawnButton(_styleProvider, _font)
            {
                Text = text,
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

            _hairStylePrevArrow.Initialize();
            _hairStyleNextArrow.Initialize();
            _hairColorPrevArrow.Initialize();
            _hairColorNextArrow.Initialize();

            _buyButton.Initialize();
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

            // Value background fields
            const int startY = 60;
            const int rowSpacing = 30;
            for (int i = 0; i < 3; i++)
            {
                var valueRect = new Rectangle(130, startY + rowSpacing * i, 106, 22);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, valueRect, _styleProvider.PanelBackgroundAlt);
            }

            _spriteBatch.End();

            // Draw children (character preview, etc.) in the render target
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
            var scaledPos = new Vector2(scaledX, scaledY);

            // Select appropriate font based on scale
            BitmapFont font;
            if (scaleFactor >= 2.0f)
                font = _contentProvider.Fonts[Constants.FontSize10];
            else if (scaleFactor >= 1.5f)
                font = _contentProvider.Fonts[Constants.FontSize09];
            else
                font = _contentProvider.Fonts[Constants.FontSize08];

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
                var titlePos = scaledPos + new Vector2(16 * scaleFactor, 8 * scaleFactor);
                spriteBatch.DrawString(font, "Barber", titlePos, _styleProvider.TitleBarText);

                // Property rows
                const int startY = 60;
                const int rowSpacing = 30;
                const int labelX = 20;

                DrawScaledPropertyRow(spriteBatch, font, scaledPos, scaleFactor, labelX, startY, "Hair Style:", GetHairStyleText());
                DrawScaledPropertyRow(spriteBatch, font, scaledPos, scaleFactor, labelX, startY + rowSpacing, "Hair Color:", GetHairColorText());
                DrawScaledPropertyRow(spriteBatch, font, scaledPos, scaleFactor, labelX, startY + rowSpacing * 2, "Cost:", GetCostText());

                // Value field borders
                for (int i = 0; i < 3; i++)
                {
                    var valueRect = new Rectangle(
                        scaledX + (int)(130 * scaleFactor),
                        scaledY + (int)((startY + rowSpacing * i) * scaleFactor),
                        (int)(106 * scaleFactor),
                        (int)(22 * scaleFactor));
                    DrawingPrimitives.DrawRectBorder(spriteBatch, valueRect, _styleProvider.PanelBorder, 1);
                }
            }
            finally
            {
                spriteBatch.End();
            }
        }

        private void DrawScaledPropertyRow(SpriteBatch spriteBatch, BitmapFont font, Vector2 scaledPos, float scaleFactor, int labelX, int y, string label, string value)
        {
            var labelPos = scaledPos + new Vector2(labelX * scaleFactor, (y + 3) * scaleFactor);
            spriteBatch.DrawString(font, label, labelPos, _styleProvider.TextPrimary);

            var valuePos = scaledPos + new Vector2(136 * scaleFactor, (y + 3) * scaleFactor);
            spriteBatch.DrawString(font, value, valuePos, _styleProvider.TextPrimary);
        }

        private string GetHairStyleText()
        {
            var style = RenderProperties.HairStyle;
            return style == 0 ? "Bald" : $"Style {style}";
        }

        private string GetHairColorText()
        {
            var idx = RenderProperties.HairColor;
            return idx >= 0 && idx < HairColorNames.Length ? HairColorNames[idx] : $"Color {idx}";
        }

        private string GetCostText()
        {
            return $"{CalculateCost()} gold";
        }

        private int CalculateCost()
        {
            var level = (int)_characterRepository.MainCharacter.Stats[CharacterStat.Level];
            return 200 + Math.Max(level - 1, 0) * 200;
        }

        private void BuyHair()
        {
            int hairStyle = RenderProperties.HairStyle;
            int hairColor = RenderProperties.HairColor;

            int totalCost = CalculateCost();
            int currentGold = _characterInventoryProvider.ItemInventory.SingleOrNone(i => i.ItemID == 1)
                                         .Map(i => i.Amount)
                                         .ValueOr(0);

            if (currentGold >= totalCost)
            {
                var message = $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_BARBER_DO_YOU_WANT_TO_BUY_A_NEW_HAIRSTYLE)}, {totalCost} {_eifFileProvider.EIFFile[1].Name}";
                var title = _localizedStringFinder.GetString(EOResourceID.DIALOG_BARBER_BUY_HAIRSTYLE);
                var msgBox = _messageBoxFactory.CreateMessageBox(message, title, EODialogButtons.OkCancel);

                msgBox.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                    {
                        _barberActions.Purchase(hairStyle, hairColor);
                        _sfxPlayer.PlaySfx(SoundEffectID.BuySell);
                    }
                };

                msgBox.ShowDialog();
            }
            else
            {
                var msgBox = _messageBoxFactory.CreateMessageBox(DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH, $" {_eifFileProvider.EIFFile[1].Name}");
                msgBox.ShowDialog();
            }
        }
    }
}
