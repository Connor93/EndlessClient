using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Provides style properties for code-drawn UI elements
    /// </summary>
    public interface IUIStyleProvider
    {
        // Panel/Dialog background
        Color PanelBackground { get; }
        Color PanelBackgroundAlt { get; }
        Color PanelBorder { get; }

        // Title bar
        Color TitleBarBackground { get; }
        Color TitleBarText { get; }

        // Buttons
        Color ButtonNormal { get; }
        Color ButtonHover { get; }
        Color ButtonPressed { get; }
        Color ButtonBorder { get; }
        Color ButtonText { get; }

        // Text
        Color TextPrimary { get; }
        Color TextSecondary { get; }
        Color TextHighlight { get; }

        // Metrics
        int CornerRadius { get; }
        int BorderThickness { get; }
        int TitleBarHeight { get; }
        int ButtonPadding { get; }
    }
}
