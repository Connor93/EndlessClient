using System;
using System.Collections.Generic;
using EndlessClient.UI.Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraSearchResultsDialog : MyraDialogAdapter, ISearchResultsDialog
    {
        private readonly IMyraFontProvider _fontProvider;
        private readonly VerticalStackPanel _listPanel;
        private readonly List<SearchResultItem> _items = new();

        private string _title = string.Empty;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                Window.Title = value;
            }
        }

        public MyraSearchResultsDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider)
            : base(uiManager, string.Empty)
        {
            _fontProvider = fontProvider;

            Window.Width = 300;
            Window.Height = 300;
            Window.TitleFont = fontProvider.Header;

            var mainPanel = new VerticalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _listPanel = new VerticalStackPanel
            {
                Spacing = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var scrollViewer = new ScrollViewer
            {
                Content = _listPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            mainPanel.Widgets.Add(scrollViewer);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            var cancelButton = new Button
            {
                Content = new Label { Text = "Cancel", Font = fontProvider.Normal },
                Width = 80,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            cancelButton.Click += (_, _) => Close(XNADialogResult.Cancel);
            mainPanel.Widgets.Add(cancelButton);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;
        }

        public void AddItem(string text, Action onClick)
        {
            _items.Add(new SearchResultItem(text, onClick));

            var label = new Label
            {
                Text = text,
                Font = _fontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(4, 2),
            };

            label.TouchDown += (_, _) =>
            {
                Close(XNADialogResult.OK);
                onClick?.Invoke();
            };

            _listPanel.Widgets.Add(label);
        }

        public void ClearItems()
        {
            _items.Clear();
            _listPanel.Widgets.Clear();
        }

        public new void Close()
        {
            Close(XNADialogResult.Cancel);
        }

        private class SearchResultItem
        {
            public string Text { get; }
            public Action OnClick { get; }

            public SearchResultItem(string text, Action onClick)
            {
                Text = text;
                OnClick = onClick;
            }
        }
    }
}
