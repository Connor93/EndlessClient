using System;
using System.Linq;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Character;
using EndlessClient.Rendering.Factories;
using EndlessClient.UI.Myra;
using EndlessClient.UIControls;
using EOLib;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Barber;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for CodeDrawnBarberDialog.
    /// Displays hair style/color selection with a character preview.
    /// The character preview is drawn after Myra via IMyraUIManager.PostRenderOverlay
    /// so it appears on top of the dialog window.
    /// </summary>
    public class MyraBarberDialog : MyraDialogAdapter, IBarberDialog
    {
        private static readonly string[] HairColorNames = { "Brown", "Green", "Pink", "Red", "Blonde", "Blue", "Purple", "Luna", "White", "Black" };

        private readonly IMyraUIManager _uiManager;
        private readonly IClientWindowSizeProvider _windowSizeProvider;
        private readonly ICharacterRepository _characterRepository;
        private readonly IBarberActions _barberActions;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;

        private readonly ICharacterRenderer _characterRenderer;
        private readonly CreateCharacterControl _characterControl;

        private readonly Label _hairStyleValue;
        private readonly Label _hairColorValue;
        private readonly Label _costValue;

        private int MaxHairStyle { get; } = 50;

        public MyraBarberDialog(IMyraUIManager uiManager,
                                IMyraFontProvider fontProvider,
                                ICharacterRendererFactory rendererFactory,
                                ICharacterRepository characterRepository,
                                IClientWindowSizeProvider windowSizeProvider,
                                ILocalizedStringFinder localizedStringFinder,
                                IBarberActions barberActions,
                                ICharacterInventoryProvider characterInventoryProvider,
                                IEIFFileProvider eifFileProvider)
            : base(uiManager, "Barber")
        {
            _uiManager = uiManager;
            _windowSizeProvider = windowSizeProvider;
            _characterRepository = characterRepository;
            _barberActions = barberActions;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _localizedStringFinder = localizedStringFinder;

            var normalFont = fontProvider.Normal;
            var headerFont = fontProvider.Header;

            // Create the character control for hair style/color state management
            var renderProperties = characterRepository.MainCharacter.RenderProperties;
            _characterControl = new CreateCharacterControl(renderProperties, rendererFactory)
            {
                MaxHairStyle = MaxHairStyle
            };

            // Get the renderer for direct drawing in PostRenderOverlay
            _characterRenderer = _characterControl.GetRenderer();

            Window.Width = 380;
            Window.TitleFont = headerFont;

            var mainGrid = new Grid();
            mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 240));
            mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            mainGrid.RowsProportions.Add(new Proportion(ProportionType.Fill));
            mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            // --- Left: Controls ---
            var controlPanel = new VerticalStackPanel
            {
                Spacing = 10,
                Padding = new Thickness(8, 8, 0, 0)
            };

            // Hair Style row
            controlPanel.Widgets.Add(CreatePropertyRow(normalFont, "Hair Style:",
                out _hairStyleValue,
                () => PrevHairStyle(),
                () => NextHairStyle()));

            // Hair Color row
            controlPanel.Widgets.Add(CreatePropertyRow(normalFont, "Hair Color:",
                out _hairColorValue,
                () => PrevHairColor(),
                () => NextHairColor()));

            // Cost row (no arrows)
            var costRow = new HorizontalStackPanel { Spacing = 8 };
            costRow.Widgets.Add(new Label
            {
                Text = "Cost:",
                Font = normalFont,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });
            _costValue = new Label
            {
                Font = normalFont,
                VerticalAlignment = VerticalAlignment.Center
            };
            costRow.Widgets.Add(_costValue);
            controlPanel.Widgets.Add(costRow);

            Grid.SetColumn(controlPanel, 0);
            Grid.SetRow(controlPanel, 0);
            mainGrid.Widgets.Add(controlPanel);

            // --- Right: Placeholder label ---
            var previewLabel = new Label
            {
                Text = "",
                Width = 99,
                Height = 123,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(previewLabel, 1);
            Grid.SetRow(previewLabel, 0);
            mainGrid.Widgets.Add(previewLabel);

            // --- Bottom: Buttons ---
            var buttonPanel = new HorizontalStackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var buyButton = new TextButton
            {
                Text = "Buy",
                Font = normalFont,
                Width = 80,
                Height = 30
            };
            buyButton.Click += (_, _) => BuyHair();

            var cancelButton = new TextButton
            {
                Text = "Cancel",
                Font = normalFont,
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (_, _) => Close(XNADialogResult.Cancel);

            buttonPanel.Widgets.Add(buyButton);
            buttonPanel.Widgets.Add(cancelButton);

            Grid.SetColumn(buttonPanel, 0);
            Grid.SetColumnSpan(buttonPanel, 2);
            Grid.SetRow(buttonPanel, 1);
            mainGrid.Widgets.Add(buttonPanel);

            Window.Content = mainGrid;

            // Clean up when dialog closes
            DialogClosing += (_, _) =>
            {
                _uiManager.PostRenderOverlay = null;
                _characterControl.Dispose();
            };

            UpdateLabels();
        }

        /// <summary>
        /// Show the dialog and register the post-render overlay for character drawing.
        /// </summary>
        public new void Show()
        {
            base.Show();

            // Initialize the control's graphics resources (render targets, textures)
            // WITHOUT adding it to Game.Components. We only need Initialize() to set up
            // the renderer — we don't want XNA to Update/Draw this control at all since
            // we handle that manually in PostRenderOverlay.
            _characterControl.Initialize();
            _characterControl.Visible = false; // Safety: prevent XNA from drawing even if somehow added

            // Warm up the renderer — the first Update() builds the character's
            // render target and textures. Without this, the first DrawCharacterOverlay()
            // may draw an incomplete sprite (timing-dependent).
            _characterRenderer.Update(new GameTime());

            // Register our draw callback — runs after Desktop.Render() so we draw ON TOP of Myra
            _uiManager.PostRenderOverlay = DrawCharacterOverlay;
        }

        private void DrawCharacterOverlay()
        {
            // Manually trigger the renderer's update (refreshes textures if needed)
            _characterRenderer.Update(new GameTime());

            // Window.Left/Top are in Myra logical coordinates. Transform to
            // backbuffer screen coordinates using the game's scale + offset.
            var scale = _windowSizeProvider.ScaleFactor;
            var offset = _windowSizeProvider.RenderOffset;

            int winX = (int)(Window.Left * scale) + offset.X;
            int winY = (int)(Window.Top * scale) + offset.Y;
            int windowWidth = (int)(Window.Bounds.Width * scale);

            if (windowWidth <= 0)
                return;

            // Right column: starts at 240px (scaled), fills remaining width
            int rightColumnStart = (int)(240 * scale);
            int rightColumnWidth = windowWidth - rightColumnStart;

            // Center the character within the right column.
            int charX = winX + rightColumnStart + (rightColumnWidth - (int)(18 * scale)) / 2;

            // Position the character's torso at 50px (scaled) below window top.
            int charY = winY + (int)(50 * scale);

            _characterRenderer.SetAbsoluteScreenPosition(charX, charY);
            _characterRenderer.Draw(new GameTime());
        }

        private HorizontalStackPanel CreatePropertyRow(
            FontStashSharp.SpriteFontBase font,
            string label,
            out Label valueLabel,
            Action onPrev,
            Action onNext)
        {
            var row = new HorizontalStackPanel { Spacing = 6 };

            row.Widgets.Add(new Label
            {
                Text = label,
                Font = font,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            var prevBtn = new TextButton
            {
                Text = "<",
                Font = font,
                Width = 36,
                Height = 28,
                Padding = new Thickness(2)
            };
            prevBtn.Click += (_, _) => onPrev();
            row.Widgets.Add(prevBtn);

            valueLabel = new Label
            {
                Font = font,
                Width = 70,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Widgets.Add(valueLabel);

            var nextBtn = new TextButton
            {
                Text = ">",
                Font = font,
                Width = 36,
                Height = 28,
                Padding = new Thickness(2)
            };
            nextBtn.Click += (_, _) => onNext();
            row.Widgets.Add(nextBtn);

            return row;
        }

        private void NextHairStyle()
        {
            _characterControl.NextHairStyle();
            UpdateLabels();
        }

        private void PrevHairStyle()
        {
            _characterControl.PrevHairStyle();
            UpdateLabels();
        }

        private void NextHairColor()
        {
            _characterControl.NextHairColor();
            UpdateLabels();
        }

        private void PrevHairColor()
        {
            _characterControl.PrevHairColor();
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            var rp = _characterControl.RenderProperties;

            _hairStyleValue.Text = rp.HairStyle == 0 ? "Bald" : $"Style {rp.HairStyle}";

            var colorIdx = rp.HairColor;
            _hairColorValue.Text = colorIdx >= 0 && colorIdx < HairColorNames.Length
                ? HairColorNames[colorIdx]
                : $"Color {colorIdx}";

            _costValue.Text = $"{CalculateCost()} gold";
        }

        private int CalculateCost()
        {
            const int BarberBase = 5;
            const int BarberStep = 5;
            var level = (int)_characterRepository.MainCharacter.Stats[CharacterStat.Level];
            return BarberBase + Math.Max(1, level) * BarberStep;
        }

        private void BuyHair()
        {
            var rp = _characterControl.RenderProperties;
            int hairStyle = rp.HairStyle;
            int hairColor = rp.HairColor;

            int totalCost = CalculateCost();
            int currentGold = _characterInventoryProvider.ItemInventory.SingleOrNone(i => i.ItemID == 1)
                                         .Map(i => i.Amount)
                                         .ValueOr(0);

            if (currentGold >= totalCost)
            {
                var message = $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_BARBER_DO_YOU_WANT_TO_BUY_A_NEW_HAIRSTYLE)}, {totalCost} {_eifFileProvider.EIFFile[1].Name}";
                var title = _localizedStringFinder.GetString(EOResourceID.DIALOG_BARBER_BUY_HAIRSTYLE);

                // Suppress character overlay so it doesn't draw on top of this modal
                _uiManager.PostRenderOverlay = null;

                var confirmDialog = new Dialog
                {
                    Title = title,
                    Content = new Label { Text = message, Wrap = true, Width = 300 },
                    Width = 350
                };
                confirmDialog.ButtonOk.Click += (_, _) =>
                {
                    _barberActions.Purchase(hairStyle, hairColor);
                    confirmDialog.Close();
                };
                confirmDialog.Closed += (_, _) =>
                {
                    // Restore the character overlay when modal closes
                    _uiManager.PostRenderOverlay = DrawCharacterOverlay;
                };
                confirmDialog.ShowModal(_uiManager.Desktop);
            }
            else
            {
                var errorMessage = _localizedStringFinder.GetString(DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH) + $" {_eifFileProvider.EIFFile[1].Name}";

                // Suppress character overlay so it doesn't draw on top of this modal
                _uiManager.PostRenderOverlay = null;

                var errorDialog = new Dialog
                {
                    Title = "Error",
                    Content = new Label { Text = errorMessage, Wrap = true, Width = 300 },
                    Width = 350
                };
                errorDialog.Closed += (_, _) =>
                {
                    _uiManager.PostRenderOverlay = DrawCharacterOverlay;
                };
                errorDialog.ShowModal(_uiManager.Desktop);
            }
        }

        public new void Dispose()
        {
            _uiManager.PostRenderOverlay = null;
            _characterRenderer?.Dispose();
            base.Dispose();
        }
    }
}
