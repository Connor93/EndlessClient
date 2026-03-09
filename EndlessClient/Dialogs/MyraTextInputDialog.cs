using System;
using EndlessClient.UI.Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for CodeDrawnTextInputDialog.
    /// Shows a prompt, text input field, and OK/Cancel buttons.
    /// </summary>
    public class MyraTextInputDialog : MyraDialogAdapter
    {
        private readonly TextBox _textBox;

        public override string ResponseText => _textBox.Text ?? string.Empty;

        public MyraTextInputDialog(IMyraUIManager uiManager,
                                   IMyraFontProvider fontProvider,
                                   string prompt,
                                   int maxInputChars = 12,
                                   bool upperCase = false)
            : base(uiManager, "Input")
        {
            var normalFont = fontProvider.Normal;
            var headerFont = fontProvider.Header;

            Window.Width = 300;
            Window.TitleFont = headerFont;

            // Main layout
            var panel = new VerticalStackPanel
            {
                Spacing = 12,
                Padding = new Thickness(4)
            };

            // Prompt label
            if (!string.IsNullOrEmpty(prompt))
            {
                var promptLabel = new Label
                {
                    Text = prompt,
                    Font = normalFont,
                    Wrap = true
                };
                panel.Widgets.Add(promptLabel);
            }

            // Text input
            _textBox = new TextBox
            {
                Font = normalFont,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Enforce max chars and optional uppercase via TextChanged
            _textBox.TextChanged += (_, _) =>
            {
                var text = _textBox.Text ?? string.Empty;
                var modified = false;

                if (text.Length > maxInputChars)
                {
                    text = text.Substring(0, maxInputChars);
                    modified = true;
                }

                if (upperCase)
                {
                    var upper = text.ToUpper();
                    if (text != upper)
                    {
                        text = upper;
                        modified = true;
                    }
                }

                if (modified)
                {
                    var cursorPos = Math.Min(_textBox.CursorPosition, text.Length);
                    _textBox.Text = text;
                    _textBox.CursorPosition = cursorPos;
                }
            };

            panel.Widgets.Add(_textBox);

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
