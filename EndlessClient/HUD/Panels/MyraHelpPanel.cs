using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based Help panel. Displays static help text.
    /// </summary>
    public class MyraHelpPanel : MyraHudPanelBase
    {
        private readonly IMyraFontProvider _fontProvider;

        public MyraHelpPanel(Game game,
                             IMyraUIManager uiManager,
                             IMyraFontProvider fontProvider)
            : base(game, uiManager, "Help")
        {
            _fontProvider = fontProvider;
        }

        public override void Initialize()
        {
            Window.Width = 484;
            Window.Height = 140;
            Window.TitleFont = _fontProvider.Large;

            var scrollViewer = new ScrollViewer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            var content = new VerticalStackPanel
            {
                Spacing = 6,
                Padding = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            AddHelpSection(content, "Movement", "Use arrow keys or click to move. Hold Left Shift to face a direction without moving.");
            AddHelpSection(content, "Chat", "Type in the chat box and press Enter. Use # prefix for commands (e.g. #help).");
            AddHelpSection(content, "Items", "Click an item in your inventory to pick it up. Click again to place it. Right-click to use or equip.");
            AddHelpSection(content, "Spells", "Click a spell in the Active Spells panel to prepare it. Click on a target to cast.");
            AddHelpSection(content, "Hotkeys", "Assign items and spells to F1–F8 (and Shift+F1–F8) in the Macro panel.");
            AddHelpSection(content, "Trading", "Use #trade [player] to request a trade. Drag items into the trade window.");

            scrollViewer.Content = content;
            Window.Content = scrollViewer;

            base.Initialize();
        }

        private void AddHelpSection(VerticalStackPanel parent, string title, string body)
        {
            parent.Widgets.Add(new Label
            {
                Text = title,
                Font = _fontProvider.Large,
                TextColor = new Color(0xD4, 0xA5, 0x37), // Gold
            });

            parent.Widgets.Add(new Label
            {
                Text = body,
                Font = _fontProvider.Normal,
                Wrap = true,
            });
        }
    }
}
