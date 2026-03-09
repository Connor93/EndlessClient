using System;
using System.Threading.Tasks;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Character;
using EndlessClient.Rendering.Factories;
using EndlessClient.UI.Myra;
using EndlessClient.UIControls;
using EOLib.Domain.Character;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based character creation dialog.
    /// Displays Name input, Gender/HairStyle/HairColor/Race arrow rows,
    /// character preview via PostRenderOverlay, and OK/Cancel buttons.
    /// </summary>
    public class MyraCreateCharacterDialog : MyraDialogAdapter, ICreateCharacterResult
    {
        private static readonly string[] GenderNames = { "Female", "Male" };
        private static readonly string[] HairColorNames = { "Brown", "Green", "Pink", "Red", "Yellow", "Purple", "Blue", "White", "Black", "Grey" };
        private static readonly string[] RaceNames = { "White", "Tan", "Pale", "Orc" };
        private const int MaxRaces = 4;

        private readonly IMyraUIManager _uiManager;
        private readonly IClientWindowSizeProvider _windowSizeProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;

        private readonly ICharacterRenderer _characterRenderer;
        private readonly CreateCharacterControl _characterControl;

        private readonly TextBox _nameInput;
        private readonly Label _genderValue;
        private readonly Label _hairStyleValue;
        private readonly Label _hairColorValue;
        private readonly Label _raceValue;

        // Expose properties for CharacterDialogActions
        public string CharacterName => _nameInput.Text?.Trim() ?? string.Empty;
        private CharacterRenderProperties RenderProperties => _characterControl.RenderProperties;
        public int Gender => RenderProperties.Gender;
        public int HairStyle => RenderProperties.HairStyle;
        public int HairColor => RenderProperties.HairColor;
        public int Race => RenderProperties.Race;

        public MyraCreateCharacterDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ICharacterRendererFactory rendererFactory,
            IEOMessageBoxFactory messageBoxFactory,
            IClientWindowSizeProvider windowSizeProvider)
            : base(uiManager, "Create Character")
        {
            _uiManager = uiManager;
            _windowSizeProvider = windowSizeProvider;
            _messageBoxFactory = messageBoxFactory;

            var normalFont = fontProvider.Normal;
            var headerFont = fontProvider.Header;

            // Create the character control for property state management
            _characterControl = new CreateCharacterControl(rendererFactory)
            {
                MaxHairStyle = 50
            };
            _characterRenderer = _characterControl.GetRenderer();

            Window.Width = 380;
            Window.TitleFont = headerFont;

            var mainGrid = new Grid();
            mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 250));
            mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            mainGrid.RowsProportions.Add(new Proportion(ProportionType.Fill));
            mainGrid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            // --- Left: Controls ---
            var controlPanel = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(8, 8, 0, 0)
            };

            // Name input row
            var nameRow = new HorizontalStackPanel { Spacing = 6 };
            nameRow.Widgets.Add(new Label
            {
                Text = "Name:",
                Font = normalFont,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });
            _nameInput = new TextBox
            {
                Font = normalFont,
                Width = 140
            };
            nameRow.Widgets.Add(_nameInput);
            controlPanel.Widgets.Add(nameRow);

            // Gender row
            controlPanel.Widgets.Add(CreatePropertyRow(normalFont, "Gender:",
                out _genderValue, NextGender, NextGender));

            // Hair Style row
            controlPanel.Widgets.Add(CreatePropertyRow(normalFont, "Hair Style:",
                out _hairStyleValue, PrevHairStyle, NextHairStyle));

            // Hair Color row
            controlPanel.Widgets.Add(CreatePropertyRow(normalFont, "Hair Color:",
                out _hairColorValue, PrevHairColor, NextHairColor));

            // Race row
            controlPanel.Widgets.Add(CreatePropertyRow(normalFont, "Race:",
                out _raceValue, NextRaceConstrained, NextRaceConstrained));

            Grid.SetColumn(controlPanel, 0);
            Grid.SetRow(controlPanel, 0);
            mainGrid.Widgets.Add(controlPanel);

            // --- Right: Placeholder for character preview (drawn via PostRenderOverlay) ---
            var previewPlaceholder = new Label
            {
                Text = "",
                Width = 99,
                Height = 123,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(previewPlaceholder, 1);
            Grid.SetRow(previewPlaceholder, 0);
            mainGrid.Widgets.Add(previewPlaceholder);

            // --- Bottom: Buttons ---
            var buttonPanel = new HorizontalStackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var okButton = new TextButton
            {
                Text = "OK",
                Font = normalFont,
                Width = 80,
                Height = 30
            };
            okButton.Click += (_, _) => ClickOk();

            var cancelButton = new TextButton
            {
                Text = "Cancel",
                Font = normalFont,
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (_, _) => Close(XNADialogResult.Cancel);

            buttonPanel.Widgets.Add(okButton);
            buttonPanel.Widgets.Add(cancelButton);

            Grid.SetColumn(buttonPanel, 0);
            Grid.SetColumnSpan(buttonPanel, 2);
            Grid.SetRow(buttonPanel, 1);
            mainGrid.Widgets.Add(buttonPanel);

            Window.Content = mainGrid;

            // Clean up overlay when dialog closes
            DialogClosing += (_, _) =>
            {
                _uiManager.PostRenderOverlay -= DrawCharacterOverlay;
                _characterControl.Dispose();
            };

            UpdateLabels();
        }

        /// <summary>
        /// Initialize the character control and register the overlay.
        /// Must be called before ShowDialogAsync() (which is dispatched via IXNADialog).
        /// </summary>
        public void InitializeOverlay()
        {
            _characterControl.Initialize();
            _characterControl.Visible = false;

            _uiManager.PostRenderOverlay += DrawCharacterOverlay;
        }

        private void DrawCharacterOverlay()
        {
            _characterRenderer.Update(new GameTime());

            var scale = _windowSizeProvider.ScaleFactor;
            var offset = _windowSizeProvider.RenderOffset;

            int winX = (int)(Window.Left * scale) + offset.X;
            int winY = (int)(Window.Top * scale) + offset.Y;
            int windowWidth = (int)(Window.Bounds.Width * scale);

            if (windowWidth <= 0)
                return;

            int rightColumnStart = (int)(250 * scale);
            int rightColumnWidth = windowWidth - rightColumnStart;

            int charX = winX + rightColumnStart + (rightColumnWidth - (int)(18 * scale)) / 2;
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

        private void NextGender()
        {
            _characterControl.NextGender();
            UpdateLabels();
        }

        private void NextHairStyle()
        {
            _characterControl.NextHairStyle();
            if (RenderProperties.HairStyle == 0) // skip bald
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

        private void NextRaceConstrained()
        {
            _characterControl.NextRace();
            if (RenderProperties.Race >= MaxRaces)
            {
                while (RenderProperties.Race != 0)
                    _characterControl.NextRace();
            }
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            var rp = RenderProperties;

            _genderValue.Text = rp.Gender >= 0 && rp.Gender < GenderNames.Length
                ? GenderNames[rp.Gender] : $"Gender {rp.Gender}";

            _hairStyleValue.Text = rp.HairStyle == 0 ? "Bald" : $"Style {rp.HairStyle}";

            var colorIdx = rp.HairColor;
            _hairColorValue.Text = colorIdx >= 0 && colorIdx < HairColorNames.Length
                ? HairColorNames[colorIdx] : $"Color {colorIdx}";

            _raceValue.Text = rp.Race >= 0 && rp.Race < RaceNames.Length
                ? RaceNames[rp.Race] : $"Race {rp.Race}";
        }

        private void ClickOk()
        {
            if (CharacterName.Length < 4)
            {
                _uiManager.PostRenderOverlay -= DrawCharacterOverlay;

                var errorDialog = new Dialog
                {
                    Title = "Error",
                    Content = new Label { Text = "Character name must be at least 4 characters.", Wrap = true, Width = 300 },
                    Width = 350
                };
                errorDialog.Closed += (_, _) =>
                {
                    _uiManager.PostRenderOverlay += DrawCharacterOverlay;
                };
                errorDialog.ShowModal(_uiManager.Desktop);
            }
            else
            {
                Close(XNADialogResult.OK);
            }
        }

        public new void Dispose()
        {
            _uiManager.PostRenderOverlay -= DrawCharacterOverlay;
            _characterRenderer?.Dispose();
            base.Dispose();
        }
    }
}
