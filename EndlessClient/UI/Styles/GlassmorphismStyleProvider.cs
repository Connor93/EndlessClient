using AutomaticTypeMapper;
using EOLib.Config;
using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Glassmorphism style - semi-transparent with subtle borders and blur simulation
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class GlassmorphismStyleProvider : IUIStyleProvider
    {
        // Panel/Dialog - semi-transparent dark with glass effect
        public Color PanelBackground => new Color(20, 25, 35, 180);
        public Color PanelBackgroundAlt => new Color(30, 35, 50, 160);
        public Color PanelBorder => new Color(100, 120, 150, 200);

        // Title bar - slightly more opaque
        public Color TitleBarBackground => new Color(40, 50, 70, 220);
        public Color TitleBarText => new Color(220, 225, 235);

        // Buttons - high opacity to prevent map bleed-through and ensure crisp text
        public Color ButtonNormal => new Color(60, 70, 100, 230);
        public Color ButtonHover => new Color(80, 95, 130, 235);
        public Color ButtonPressed => new Color(40, 50, 70, 240);
        public Color ButtonBorder => new Color(90, 110, 140, 220);
        public Color ButtonText => new Color(220, 225, 235);

        // Text
        public Color TextPrimary => new Color(230, 235, 245);
        public Color TextSecondary => new Color(160, 170, 190);
        public Color TextHighlight => new Color(100, 200, 255); // Cyan for visibility

        // Status Bars - vibrant colors with glass effect
        public Color StatusBarBackground => new Color(15, 20, 30, 200);
        public Color StatusBarBorder => new Color(80, 100, 130, 180);
        public Color HPBarFill => new Color(220, 60, 80, 220);  // Red
        public Color TPBarFill => new Color(100, 180, 80, 220); // Green
        public Color SPBarFill => new Color(70, 130, 220, 220); // Blue
        public Color TNLBarFill => new Color(220, 180, 60, 220); // Yellow/Gold

        // Metrics
        public int CornerRadius => 6;
        public int BorderThickness => 1;
        public int TitleBarHeight => 28;
        public int ButtonPadding => 12;

        // Toast notifications - semi-transparent with glass effect
        public Color ToastInfoBackground => new Color(40, 80, 140, 220);
        public Color ToastInfoBorder => new Color(80, 140, 200, 200);
        public Color ToastWarningBackground => new Color(160, 80, 40, 220);
        public Color ToastWarningBorder => new Color(220, 120, 60, 200);
        public Color ToastActionBackground => new Color(40, 120, 80, 220);
        public Color ToastActionBorder => new Color(80, 180, 120, 200);
        public Color ToastGuildBackground => new Color(140, 110, 30, 220);
        public Color ToastGuildBorder => new Color(210, 180, 60, 200);

        // Grid tile - glass effect tiles
        public Color GridTileBackground => new Color(30, 40, 60, 160);
        public Color GridTileBorder => new Color(80, 100, 130, 180);
        public Color GridTileHover => new Color(60, 80, 120, 200);

        // Tab - glass tabs
        public Color TabActive => new Color(60, 80, 120, 220);
        public Color TabInactive => new Color(30, 40, 60, 160);
        public Color TabText => new Color(220, 225, 235);

        // Input - glass input fields
        public Color InputBackground => new Color(15, 20, 30, 200);
        public Color InputBorder => new Color(80, 100, 130, 180);
        public Color InputText => new Color(230, 235, 245);
        public Color InputPlaceholder => new Color(120, 130, 150);

        // Tooltip - slightly brighter glass
        public Color TooltipBackground => new Color(25, 30, 45, 230);
        public Color TooltipBorder => new Color(100, 130, 170, 220);
        public Color TooltipText => new Color(230, 235, 245);

        // Slots - glass effect slots
        public Color SlotBackground => new Color(20, 28, 45, 200);
        public Color SlotBackgroundAlt => new Color(25, 35, 55, 180);
        public Color SlotBorder => new Color(60, 80, 110, 180);

        // List rows
        public Color ListRowEven => new Color(30, 40, 65, 130);
        public Color ListRowOdd => new Color(25, 35, 55, 130);
        public Color ListRowHover => new Color(100, 150, 220, 40);
        public Color ListHeaderBackground => new Color(40, 55, 80, 220);

        // Scrollbar
        public Color ScrollTrackBackground => new Color(15, 20, 35, 200);
        public Color ScrollTrackBorder => new Color(60, 80, 110, 180);
        public Color ScrollThumbBorder => new Color(100, 130, 170, 200);

        // Misc UI
        public Color ButtonDisabled => new Color(40, 50, 70, 160);
        public Color SectionBackground => new Color(10, 15, 25, 100);
        public Color OverlayDim => new Color(0, 0, 0, 30);
        public Color BadgeBackground => new Color(0, 0, 0, 160);

        // Progress bar
        public Color ProgressBarBackground => new Color(15, 20, 35, 200);
        public Color ProgressBarFill => new Color(70, 140, 220, 220);

        // Semantic colors
        public Color CompletionColor => new Color(100, 220, 120);
        public Color AgreementColor => new Color(100, 255, 120);
        public Color DangerColor => new Color(200, 80, 80);
        public Color GoldColor => new Color(255, 215, 50);
        public Color LinkHoverColor => new Color(150, 220, 255);

        // Chat text colors
        public Color ChatDefault => Color.Black;
        public Color ChatServer => Color.FromNonPremultiplied(0x8a, 0x5c, 0x4a, 0xff);
        public Color ChatError => Color.FromNonPremultiplied(0x7d, 0x0a, 0x0a, 0xff);
        public Color ChatPM => Color.FromNonPremultiplied(0x5a, 0x3c, 0x00, 0xff);
        public Color ChatServerGlobal => Color.FromNonPremultiplied(0x8a, 0x6d, 0x00, 0xff);
        public Color ChatAdmin => Color.FromNonPremultiplied(0x7a, 0x4a, 0x2a, 0xff);
    }
}
