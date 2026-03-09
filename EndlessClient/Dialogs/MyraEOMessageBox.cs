using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based message box with optional caption, message text, and OK/Cancel buttons.
    /// Replaces the XNA-based EOMessageBox so it renders in the Myra layer.
    /// </summary>
    public class MyraEOMessageBox : MyraDialogAdapter
    {
        public MyraEOMessageBox(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            string message,
            string caption = "",
            EODialogButtons whichButtons = EODialogButtons.Ok)
            : base(uiManager, string.IsNullOrEmpty(caption) ? "Notice" : caption)
        {
            Window.Width = 320;
            Window.TitleFont = fontProvider.Header;

            var messageLabel = new Label
            {
                Text = message,
                Font = fontProvider.Normal,
                TextColor = new Color(220, 220, 230),
                Wrap = true,
                Padding = new Thickness(8, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var buttonBar = new HorizontalStackPanel
            {
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(0, 8),
            };

            switch (whichButtons)
            {
                case EODialogButtons.Ok:
                    buttonBar.Widgets.Add(CreateButton(fontProvider, "OK", XNADialogResult.OK));
                    break;
                case EODialogButtons.Cancel:
                    buttonBar.Widgets.Add(CreateButton(fontProvider, "Cancel", XNADialogResult.Cancel));
                    break;
                case EODialogButtons.OkCancel:
                    buttonBar.Widgets.Add(CreateButton(fontProvider, "OK", XNADialogResult.OK));
                    buttonBar.Widgets.Add(CreateButton(fontProvider, "Cancel", XNADialogResult.Cancel));
                    break;
            }

            var layout = new VerticalStackPanel { Spacing = 4 };
            layout.Widgets.Add(messageLabel);
            layout.Widgets.Add(buttonBar);

            Window.Content = layout;
        }

        private Button CreateButton(IMyraFontProvider fontProvider, string label, XNADialogResult result)
        {
            var btn = new Button
            {
                Content = new Label { Text = label, Font = fontProvider.Normal },
                Width = 80,
                Height = 28,
                Background = new SolidBrush(new Color(60, 60, 80)),
                OverBackground = new SolidBrush(new Color(80, 80, 110)),
                Border = new SolidBrush(new Color(100, 100, 130)),
                BorderThickness = new Thickness(1),
            };
            btn.Click += (_, _) => Close(result);
            return btn;
        }
    }
}
