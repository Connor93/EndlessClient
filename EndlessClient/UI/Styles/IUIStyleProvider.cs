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
        Color ToastGuildBackground { get; }
        Color ToastGuildBorder { get; }

        // Grid tile styling
        Color GridTileBackground { get; }
        Color GridTileBorder { get; }
        Color GridTileHover { get; }

        // Tab/category styling
        Color TabActive { get; }
        Color TabInactive { get; }
        Color TabText { get; }

        // Search/input styling
        Color InputBackground { get; }
        Color InputBorder { get; }
        Color InputText { get; }
        Color InputPlaceholder { get; }

        // Tooltip styling
        Color TooltipBackground { get; }
        Color TooltipBorder { get; }
        Color TooltipText { get; }

        // Slot styling (spells, macros, inventory grid cells)
        Color SlotBackground { get; }
        Color SlotBackgroundAlt { get; }
        Color SlotBorder { get; }

        // List/table row styling
        Color ListRowEven { get; }
        Color ListRowOdd { get; }
        Color ListRowHover { get; }
        Color ListHeaderBackground { get; }

        // Scrollbar styling
        Color ScrollTrackBackground { get; }
        Color ScrollTrackBorder { get; }
        Color ScrollThumbBorder { get; }

        // Misc UI elements
        Color ButtonDisabled { get; }
        Color SectionBackground { get; }
        Color OverlayDim { get; }
        Color BadgeBackground { get; }

        // Generic progress bar (non-stat)
        Color ProgressBarBackground { get; }
        Color ProgressBarFill { get; }

        // Semantic colors
        Color CompletionColor { get; }
        Color AgreementColor { get; }
        Color DangerColor { get; }
        Color GoldColor { get; }
        Color LinkHoverColor { get; }

        // Chat text colors
        Color ChatDefault { get; }
        Color ChatServer { get; }
        Color ChatError { get; }
        Color ChatPM { get; }
        Color ChatServerGlobal { get; }
        Color ChatAdmin { get; }
    }
}
