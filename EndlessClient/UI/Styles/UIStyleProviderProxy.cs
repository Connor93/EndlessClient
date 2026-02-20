using AutomaticTypeMapper;
using EOLib.Config;
using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Lazy proxy for IUIStyleProvider that delegates to UIStyleProviderFactory.
    /// This ensures the correct theme provider is used based on config, which is
    /// loaded after DI registration. Without this proxy, the auto-mapper would
    /// register whichever concrete provider it scans last as IUIStyleProvider.
    /// </summary>
    [MappedType(BaseType = typeof(IUIStyleProvider), IsSingleton = true)]
    public class UIStyleProviderProxy : IUIStyleProvider
    {
        private readonly IUIStyleProviderFactory _factory;
        private IUIStyleProvider _inner;

        private IUIStyleProvider Inner => _inner ??= _factory.Create();

        public UIStyleProviderProxy(IUIStyleProviderFactory factory)
        {
            _factory = factory;
        }

        // Panel/Dialog
        public Color PanelBackground => Inner.PanelBackground;
        public Color PanelBackgroundAlt => Inner.PanelBackgroundAlt;
        public Color PanelBorder => Inner.PanelBorder;

        // Title bar
        public Color TitleBarBackground => Inner.TitleBarBackground;
        public Color TitleBarText => Inner.TitleBarText;

        // Buttons
        public Color ButtonNormal => Inner.ButtonNormal;
        public Color ButtonHover => Inner.ButtonHover;
        public Color ButtonPressed => Inner.ButtonPressed;
        public Color ButtonBorder => Inner.ButtonBorder;
        public Color ButtonText => Inner.ButtonText;

        // Text
        public Color TextPrimary => Inner.TextPrimary;
        public Color TextSecondary => Inner.TextSecondary;
        public Color TextHighlight => Inner.TextHighlight;

        // Status Bars
        public Color StatusBarBackground => Inner.StatusBarBackground;
        public Color StatusBarBorder => Inner.StatusBarBorder;
        public Color HPBarFill => Inner.HPBarFill;
        public Color TPBarFill => Inner.TPBarFill;
        public Color SPBarFill => Inner.SPBarFill;
        public Color TNLBarFill => Inner.TNLBarFill;

        // Metrics
        public int CornerRadius => Inner.CornerRadius;
        public int BorderThickness => Inner.BorderThickness;
        public int TitleBarHeight => Inner.TitleBarHeight;
        public int ButtonPadding => Inner.ButtonPadding;

        // Toast notifications
        public Color ToastInfoBackground => Inner.ToastInfoBackground;
        public Color ToastInfoBorder => Inner.ToastInfoBorder;
        public Color ToastWarningBackground => Inner.ToastWarningBackground;
        public Color ToastWarningBorder => Inner.ToastWarningBorder;
        public Color ToastActionBackground => Inner.ToastActionBackground;
        public Color ToastActionBorder => Inner.ToastActionBorder;
        public Color ToastGuildBackground => Inner.ToastGuildBackground;
        public Color ToastGuildBorder => Inner.ToastGuildBorder;

        // Grid tile
        public Color GridTileBackground => Inner.GridTileBackground;
        public Color GridTileBorder => Inner.GridTileBorder;
        public Color GridTileHover => Inner.GridTileHover;

        // Tab
        public Color TabActive => Inner.TabActive;
        public Color TabInactive => Inner.TabInactive;
        public Color TabText => Inner.TabText;

        // Input
        public Color InputBackground => Inner.InputBackground;
        public Color InputBorder => Inner.InputBorder;
        public Color InputText => Inner.InputText;
        public Color InputPlaceholder => Inner.InputPlaceholder;

        // Tooltip
        public Color TooltipBackground => Inner.TooltipBackground;
        public Color TooltipBorder => Inner.TooltipBorder;
        public Color TooltipText => Inner.TooltipText;

        // Slots
        public Color SlotBackground => Inner.SlotBackground;
        public Color SlotBackgroundAlt => Inner.SlotBackgroundAlt;
        public Color SlotBorder => Inner.SlotBorder;

        // List rows
        public Color ListRowEven => Inner.ListRowEven;
        public Color ListRowOdd => Inner.ListRowOdd;
        public Color ListRowHover => Inner.ListRowHover;
        public Color ListHeaderBackground => Inner.ListHeaderBackground;

        // Scrollbar
        public Color ScrollTrackBackground => Inner.ScrollTrackBackground;
        public Color ScrollTrackBorder => Inner.ScrollTrackBorder;
        public Color ScrollThumbBorder => Inner.ScrollThumbBorder;

        // Misc UI
        public Color ButtonDisabled => Inner.ButtonDisabled;
        public Color SectionBackground => Inner.SectionBackground;
        public Color OverlayDim => Inner.OverlayDim;
        public Color BadgeBackground => Inner.BadgeBackground;

        // Progress bar
        public Color ProgressBarBackground => Inner.ProgressBarBackground;
        public Color ProgressBarFill => Inner.ProgressBarFill;

        // Semantic colors
        public Color CompletionColor => Inner.CompletionColor;
        public Color AgreementColor => Inner.AgreementColor;
        public Color DangerColor => Inner.DangerColor;
        public Color GoldColor => Inner.GoldColor;
        public Color LinkHoverColor => Inner.LinkHoverColor;

        // Chat text colors
        public Color ChatDefault => Inner.ChatDefault;
        public Color ChatServer => Inner.ChatServer;
        public Color ChatError => Inner.ChatError;
        public Color ChatPM => Inner.ChatPM;
        public Color ChatServerGlobal => Inner.ChatServerGlobal;
        public Color ChatAdmin => Inner.ChatAdmin;
    }
}
