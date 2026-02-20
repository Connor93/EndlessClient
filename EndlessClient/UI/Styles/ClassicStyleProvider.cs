using AutomaticTypeMapper;
using EOLib.Config;
using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Classic style - beveled 3D borders like old Windows
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class ClassicStyleProvider : IUIStyleProvider
    {
        // Panel/Dialog - gray tones
        public Color PanelBackground => new Color(192, 192, 192);
        public Color PanelBackgroundAlt => new Color(212, 212, 212);
        public Color PanelBorder => new Color(64, 64, 64);

        // Title bar - blue gradient simulation
        public Color TitleBarBackground => new Color(0, 85, 165);
        public Color TitleBarText => Color.White;

        // Buttons - raised appearance
        public Color ButtonNormal => new Color(192, 192, 192);
        public Color ButtonHover => new Color(212, 212, 212);
        public Color ButtonPressed => new Color(172, 172, 172);
        public Color ButtonBorder => new Color(128, 128, 128);
        public Color ButtonText => Color.Black;

        // Text
        public Color TextPrimary => Color.Black;
        public Color TextSecondary => new Color(64, 64, 64);
        public Color TextHighlight => new Color(0, 0, 128);

        // Status Bars - classic Windows-style colors
        public Color StatusBarBackground => new Color(128, 128, 128);
        public Color StatusBarBorder => new Color(64, 64, 64);
        public Color HPBarFill => new Color(255, 0, 0);     // Pure red
        public Color TPBarFill => new Color(0, 192, 0);     // Pure green
        public Color SPBarFill => new Color(0, 0, 255);     // Pure blue
        public Color TNLBarFill => new Color(255, 255, 0);  // Pure yellow

        // Metrics
        public int CornerRadius => 0;
        public int BorderThickness => 2;
        public int TitleBarHeight => 22;
        public int ButtonPadding => 8;

        // Toast notifications - classic Windows-style colors
        public Color ToastInfoBackground => new Color(240, 240, 240);
        public Color ToastInfoBorder => new Color(0, 0, 128);
        public Color ToastWarningBackground => new Color(255, 240, 200);
        public Color ToastWarningBorder => new Color(192, 0, 0);
        public Color ToastActionBackground => new Color(240, 255, 240);
        public Color ToastActionBorder => new Color(0, 128, 0);
        public Color ToastGuildBackground => new Color(255, 245, 220);
        public Color ToastGuildBorder => new Color(180, 140, 30);

        // Grid tile - classic raised tiles
        public Color GridTileBackground => new Color(212, 212, 212);
        public Color GridTileBorder => new Color(128, 128, 128);
        public Color GridTileHover => new Color(230, 230, 230);

        // Tab - classic tabs
        public Color TabActive => new Color(212, 212, 212);
        public Color TabInactive => new Color(172, 172, 172);
        public Color TabText => Color.Black;

        // Input - classic sunken input
        public Color InputBackground => Color.White;
        public Color InputBorder => new Color(128, 128, 128);
        public Color InputText => Color.Black;
        public Color InputPlaceholder => new Color(160, 160, 160);

        // Tooltip - classic tooltip
        public Color TooltipBackground => new Color(255, 255, 225);
        public Color TooltipBorder => Color.Black;
        public Color TooltipText => Color.Black;

        // Slots - classic recessed slots
        public Color SlotBackground => new Color(180, 180, 180);
        public Color SlotBackgroundAlt => new Color(190, 190, 190);
        public Color SlotBorder => new Color(128, 128, 128);

        // List rows
        public Color ListRowEven => new Color(220, 220, 220);
        public Color ListRowOdd => new Color(210, 210, 210);
        public Color ListRowHover => new Color(0, 0, 128, 30);
        public Color ListHeaderBackground => new Color(192, 192, 192);

        // Scrollbar
        public Color ScrollTrackBackground => new Color(200, 200, 200);
        public Color ScrollTrackBorder => new Color(128, 128, 128);
        public Color ScrollThumbBorder => new Color(96, 96, 96);

        // Misc UI
        public Color ButtonDisabled => new Color(160, 160, 160);
        public Color SectionBackground => new Color(0, 0, 0, 30);
        public Color OverlayDim => new Color(0, 0, 0, 15);
        public Color BadgeBackground => new Color(0, 0, 0, 120);

        // Progress bar
        public Color ProgressBarBackground => new Color(192, 192, 192);
        public Color ProgressBarFill => new Color(0, 0, 128);

        // Semantic colors
        public Color CompletionColor => new Color(0, 160, 0);
        public Color AgreementColor => new Color(0, 200, 0);
        public Color DangerColor => new Color(192, 0, 0);
        public Color GoldColor => new Color(200, 170, 0);
        public Color LinkHoverColor => new Color(0, 0, 255);

        // Chat text colors
        public Color ChatDefault => Color.Black;
        public Color ChatServer => Color.FromNonPremultiplied(0x8a, 0x5c, 0x4a, 0xff);
        public Color ChatError => Color.FromNonPremultiplied(0x7d, 0x0a, 0x0a, 0xff);
        public Color ChatPM => Color.FromNonPremultiplied(0x5a, 0x3c, 0x00, 0xff);
        public Color ChatServerGlobal => Color.FromNonPremultiplied(0x8a, 0x6d, 0x00, 0xff);
        public Color ChatAdmin => Color.FromNonPremultiplied(0x7a, 0x4a, 0x2a, 0xff);
    }
}
