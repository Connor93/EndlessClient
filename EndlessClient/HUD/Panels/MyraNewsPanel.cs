using System.Collections.Generic;
using System.Linq;
using EndlessClient.UI.Myra;
using EOLib.Domain.Login;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based News panel. Displays server news text in a scrollable list.
    /// </summary>
    public class MyraNewsPanel : MyraHudPanelBase
    {
        private readonly INewsProvider _newsProvider;
        private readonly IMyraFontProvider _fontProvider;

        private VerticalStackPanel _contentPanel;
        private readonly List<string> _cachedNewsStrings = new();

        public MyraNewsPanel(Game game,
                             IMyraUIManager uiManager,
                             IMyraFontProvider fontProvider,
                             INewsProvider newsProvider)
            : base(game, uiManager, "Server News")
        {
            _fontProvider = fontProvider;
            _newsProvider = newsProvider;
        }

        public override void Initialize()
        {
            Window.Width = 484;
            Window.Height = 210;
            Window.TitleFont = _fontProvider.Large;

            var scrollViewer = new ScrollViewer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            _contentPanel = new VerticalStackPanel
            {
                Spacing = 2,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            scrollViewer.Content = _contentPanel;
            Window.Content = scrollViewer;

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            if (Window.Visible && !_cachedNewsStrings.SequenceEqual(_newsProvider.NewsText))
            {
                _cachedNewsStrings.Clear();
                _cachedNewsStrings.AddRange(_newsProvider.NewsText);
                RebuildContent();
            }

            base.Update(gameTime);
        }

        private void RebuildContent()
        {
            _contentPanel.Widgets.Clear();

            // Header
            if (!string.IsNullOrEmpty(_newsProvider.NewsHeader))
            {
                _contentPanel.Widgets.Add(new Label
                {
                    Text = _newsProvider.NewsHeader,
                    Font = _fontProvider.Large,
                    TextColor = new Color(0xD4, 0xA5, 0x37), // Gold header
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Wrap = true,
                });

                _contentPanel.Widgets.Add(new HorizontalSeparator());
            }

            // News lines
            foreach (var line in _cachedNewsStrings)
            {
                _contentPanel.Widgets.Add(new Label
                {
                    Text = line,
                    Font = _fontProvider.Normal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Wrap = true,
                });
            }
        }
    }
}
