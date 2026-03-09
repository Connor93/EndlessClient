using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.UI.Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for CodeDrawnTextMultiInputDialog.
    /// Shows a prompt, multiple labeled text inputs with optional restrictions, and OK/Cancel buttons.
    /// </summary>
    public class MyraTextMultiInputDialog : MyraDialogAdapter, ITextMultiInputDialog
    {
        private readonly TextBox[] _textBoxes;

        public IReadOnlyList<string> Responses => _textBoxes.Select(t => t.Text ?? string.Empty).ToList();

        public MyraTextMultiInputDialog(IMyraUIManager uiManager,
                                        IMyraFontProvider fontProvider,
                                        string title,
                                        string prompt,
                                        TextMultiInputDialog.InputInfo[] inputInfo)
            : base(uiManager, title)
        {
            var normalFont = fontProvider.Normal;
            var headerFont = fontProvider.Header;

            Window.Width = 340;
            Window.TitleFont = headerFont;

            _textBoxes = new TextBox[inputInfo.Length];

            // Main layout
            var panel = new VerticalStackPanel
            {
                Spacing = 8,
                Padding = new Thickness(4)
            };

            // Prompt label
            if (!string.IsNullOrEmpty(prompt))
            {
                panel.Widgets.Add(new Label
                {
                    Text = prompt,
                    Font = normalFont,
                    Wrap = true
                });
            }

            // Input rows
            for (int i = 0; i < inputInfo.Length; i++)
            {
                var info = inputInfo[i];

                var rowPanel = new VerticalStackPanel { Spacing = 2 };

                // Label
                rowPanel.Widgets.Add(new Label
                {
                    Text = info.Label,
                    Font = normalFont
                });

                // TextBox
                var textBox = new TextBox
                {
                    Font = normalFont,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Text = info.DefaultValue ?? string.Empty
                };

                // Capture for closure
                var maxChars = info.MaxChars;
                var restriction = info.InputRestriction;

                textBox.TextChanged += (_, _) =>
                {
                    var text = textBox.Text ?? string.Empty;
                    var modified = false;

                    // Max chars
                    if (text.Length > maxChars)
                    {
                        text = text.Substring(0, maxChars);
                        modified = true;
                    }

                    // Input restriction
                    switch (restriction)
                    {
                        case TextMultiInputDialog.InputInfo.InputRestrict.Uppercase:
                            var upper = text.ToUpper();
                            if (text != upper) { text = upper; modified = true; }
                            break;
                        case TextMultiInputDialog.InputInfo.InputRestrict.Numeric:
                            var filtered = new string(text.Where(char.IsDigit).ToArray());
                            if (text != filtered) { text = filtered; modified = true; }
                            break;
                    }

                    if (modified)
                    {
                        var cursorPos = Math.Min(textBox.CursorPosition, text.Length);
                        textBox.Text = text;
                        textBox.CursorPosition = cursorPos;
                    }
                };

                rowPanel.Widgets.Add(textBox);
                panel.Widgets.Add(rowPanel);

                _textBoxes[i] = textBox;
            }

            // Button row
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
