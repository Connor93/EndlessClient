using System;
using System.Linq;
using EndlessClient.UI.Myra;
using EOLib.Localization;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for CodeDrawnItemTransferDialog.
    /// Shows a prompt, a slider + text box for amount selection, and OK/Cancel buttons.
    /// </summary>
    public class MyraItemTransferDialog : MyraDialogAdapter, IItemTransferDialog
    {
        private readonly int _totalAmount;
        private readonly TextBox _amountBox;
        private readonly HorizontalSlider _slider;
        private bool _suppressSync;

        public int SelectedAmount
        {
            get
            {
                int.TryParse(_amountBox.Text, out var val);
                return Math.Max(1, Math.Min(val, _totalAmount));
            }
        }

        public MyraItemTransferDialog(IMyraUIManager uiManager,
                                      IMyraFontProvider fontProvider,
                                      ILocalizedStringFinder localizedStringFinder,
                                      string itemName,
                                      ItemTransferType transferType,
                                      int totalAmount,
                                      EOResourceID message)
            : base(uiManager, "Transfer")
        {
            _totalAmount = totalAmount;

            var normalFont = fontProvider.Normal;
            var headerFont = fontProvider.Header;

            Window.Width = 300;
            Window.TitleFont = headerFont;

            var prompt = $"{localizedStringFinder.GetString(EOResourceID.DIALOG_TRANSFER_HOW_MUCH)} {itemName} {localizedStringFinder.GetString(message)}";

            var panel = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(4)
            };

            // Prompt
            panel.Widgets.Add(new Label
            {
                Text = prompt,
                Font = normalFont,
                Wrap = true
            });

            // Slider
            _slider = new HorizontalSlider
            {
                Minimum = 1,
                Maximum = totalAmount,
                Value = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            _slider.ValueChanged += (_, _) =>
            {
                if (_suppressSync) return;
                _suppressSync = true;
                _amountBox.Text = ((int)Math.Round(_slider.Value)).ToString();
                _suppressSync = false;
            };

            panel.Widgets.Add(_slider);

            // Amount row: label + textbox
            var amountRow = new HorizontalStackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            amountRow.Widgets.Add(new Label
            {
                Text = "Amount:",
                Font = normalFont,
                VerticalAlignment = VerticalAlignment.Center
            });

            _amountBox = new TextBox
            {
                Text = "1",
                Font = normalFont,
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            _amountBox.TextChanged += (_, _) =>
            {
                if (_suppressSync) return;

                var text = _amountBox.Text ?? string.Empty;

                // Filter to digits only
                var filtered = new string(text.Where(char.IsDigit).ToArray());
                if (filtered != text)
                {
                    _amountBox.Text = filtered;
                    return; // Will re-enter
                }

                if (int.TryParse(filtered, out var amt))
                {
                    if (amt > _totalAmount)
                    {
                        _amountBox.Text = _totalAmount.ToString();
                        return; // Will re-enter
                    }

                    _suppressSync = true;
                    _slider.Value = amt;
                    _suppressSync = false;
                }
            };

            amountRow.Widgets.Add(_amountBox);
            panel.Widgets.Add(amountRow);

            // Buttons
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
            okButton.Click += (_, _) => Close(XNAControls.XNADialogResult.OK);

            var cancelButton = new TextButton
            {
                Text = "Cancel",
                Font = normalFont,
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (_, _) => Close(XNAControls.XNADialogResult.Cancel);

            buttonPanel.Widgets.Add(okButton);
            buttonPanel.Widgets.Add(cancelButton);
            panel.Widgets.Add(buttonPanel);

            Window.Content = panel;
        }
    }
}
