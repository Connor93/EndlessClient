using AutomaticTypeMapper;
using EOLib.Config;
using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Flat style - solid colors, clean modern look
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class FlatStyleProvider : IUIStyleProvider
    {
        // Panel/Dialog - solid dark
        public Color PanelBackground => new Color(35, 40, 50);
        public Color PanelBackgroundAlt => new Color(45, 50, 65);
        public Color PanelBorder => new Color(70, 80, 100);

        // Title bar
        public Color TitleBarBackground => new Color(55, 65, 85);
        public Color TitleBarText => Color.White;

        // Buttons
        public Color ButtonNormal => new Color(70, 85, 115);
        public Color ButtonHover => new Color(90, 110, 145);
        public Color ButtonPressed => new Color(50, 60, 80);
        public Color ButtonBorder => new Color(90, 105, 135);
        public Color ButtonText => Color.White;

        // Text
        public Color TextPrimary => Color.White;
        public Color TextSecondary => new Color(180, 190, 210);
        public Color TextHighlight => new Color(100, 200, 255);

        // Status Bars - solid vibrant colors
        public Color StatusBarBackground => new Color(25, 30, 40);
        public Color StatusBarBorder => new Color(60, 70, 90);
        public Color HPBarFill => new Color(200, 50, 70);   // Red
        public Color TPBarFill => new Color(80, 170, 60);   // Green
        public Color SPBarFill => new Color(60, 120, 200);  // Blue
        public Color TNLBarFill => new Color(200, 160, 50); // Yellow/Gold

        // Metrics
        public int CornerRadius => 4;
        public int BorderThickness => 1;
        public int TitleBarHeight => 26;
        public int ButtonPadding => 10;

        // Toast notifications - solid flat colors
        public Color ToastInfoBackground => new Color(45, 100, 170);
        public Color ToastInfoBorder => new Color(70, 130, 200);
        public Color ToastWarningBackground => new Color(180, 90, 50);
        public Color ToastWarningBorder => new Color(220, 120, 70);
        public Color ToastActionBackground => new Color(50, 140, 90);
        public Color ToastActionBorder => new Color(80, 180, 120);
        public Color ToastGuildBackground => new Color(160, 130, 40);
        public Color ToastGuildBorder => new Color(220, 190, 70);

        // Grid tile - solid flat tiles
        public Color GridTileBackground => new Color(45, 55, 70);
        public Color GridTileBorder => new Color(60, 70, 90);
        public Color GridTileHover => new Color(65, 80, 105);

        // Tab - flat tabs
        public Color TabActive => new Color(70, 85, 115);
        public Color TabInactive => new Color(45, 55, 70);
        public Color TabText => Color.White;

        // Input - flat input fields
        public Color InputBackground => new Color(25, 30, 40);
        public Color InputBorder => new Color(60, 70, 90);
        public Color InputText => Color.White;
        public Color InputPlaceholder => new Color(130, 140, 160);

        // Tooltip - flat tooltip
        public Color TooltipBackground => new Color(35, 40, 55);
        public Color TooltipBorder => new Color(70, 85, 110);
        public Color TooltipText => Color.White;

        // Slots - flat solid slots
        public Color SlotBackground => new Color(28, 32, 42);
        public Color SlotBackgroundAlt => new Color(32, 38, 50);
        public Color SlotBorder => new Color(55, 65, 85);

        // List rows
        public Color ListRowEven => new Color(40, 48, 62, 150);
        public Color ListRowOdd => new Color(35, 42, 55, 150);
        public Color ListRowHover => new Color(255, 255, 255, 25);
        public Color ListHeaderBackground => new Color(50, 60, 80, 230);

        // Scrollbar
        public Color ScrollTrackBackground => new Color(25, 30, 40);
        public Color ScrollTrackBorder => new Color(55, 65, 85);
        public Color ScrollThumbBorder => new Color(90, 105, 135);

        // Misc UI
        public Color ButtonDisabled => new Color(45, 52, 68);
        public Color SectionBackground => new Color(0, 0, 0, 50);
        public Color OverlayDim => new Color(0, 0, 0, 20);
        public Color BadgeBackground => new Color(0, 0, 0, 140);

        // Progress bar
        public Color ProgressBarBackground => new Color(25, 30, 40);
        public Color ProgressBarFill => new Color(60, 130, 210);

        // Semantic colors
        public Color CompletionColor => new Color(100, 200, 100);
        public Color AgreementColor => new Color(100, 255, 100);
        public Color DangerColor => new Color(200, 70, 70);
        public Color GoldColor => new Color(255, 215, 0);
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
