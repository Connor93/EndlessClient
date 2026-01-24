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

        // Status Bars (HP, TP, SP, TNL)
        Color StatusBarBackground { get; }
        Color StatusBarBorder { get; }
        Color HPBarFill { get; }
        Color TPBarFill { get; }
        Color SPBarFill { get; }
        Color TNLBarFill { get; }

        // Metrics
        int CornerRadius { get; }
        int BorderThickness { get; }
        int TitleBarHeight { get; }
        int ButtonPadding { get; }

        // Toast notifications
        Color ToastInfoBackground { get; }
        Color ToastInfoBorder { get; }
        Color ToastWarningBackground { get; }
        Color ToastWarningBorder { get; }
        Color ToastActionBackground { get; }
        Color ToastActionBorder { get; }
    }
}
