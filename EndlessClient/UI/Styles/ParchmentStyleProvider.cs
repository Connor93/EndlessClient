using AutomaticTypeMapper;
using EOLib.Config;
using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Parchment style - warm beige/cream RPG-style with brown accents
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class ParchmentStyleProvider : IUIStyleProvider
    {
        // Panel/Dialog - warm cream parchment
        public Color PanelBackground => new Color(240, 228, 205);
        public Color PanelBackgroundAlt => new Color(230, 215, 190);
        public Color PanelBorder => new Color(120, 90, 60);

        // Title bar - warm tan
        public Color TitleBarBackground => new Color(180, 155, 120);
        public Color TitleBarText => new Color(50, 30, 15);

        // Buttons - warm beige with brown borders
        public Color ButtonNormal => new Color(215, 200, 175);
        public Color ButtonHover => new Color(230, 215, 185);
        public Color ButtonPressed => new Color(195, 180, 155);
        public Color ButtonBorder => new Color(140, 110, 75);
        public Color ButtonText => new Color(60, 40, 20);

        // Text - brown tones
        public Color TextPrimary => new Color(50, 35, 20);
        public Color TextSecondary => new Color(120, 95, 65);
        public Color TextHighlight => new Color(140, 60, 20);

        // Status Bars - warm-toned
        public Color StatusBarBackground => new Color(200, 185, 160);
        public Color StatusBarBorder => new Color(140, 110, 75);
        public Color HPBarFill => new Color(190, 55, 55);
        public Color TPBarFill => new Color(75, 155, 65);
        public Color SPBarFill => new Color(65, 120, 185);
        public Color TNLBarFill => new Color(200, 165, 50);

        // Metrics
        public int CornerRadius => 3;
        public int BorderThickness => 2;
        public int TitleBarHeight => 26;
        public int ButtonPadding => 10;

        // Toast notifications - warm parchment tones
        public Color ToastInfoBackground => new Color(220, 210, 185);
        public Color ToastInfoBorder => new Color(100, 120, 160);
        public Color ToastWarningBackground => new Color(240, 215, 180);
        public Color ToastWarningBorder => new Color(180, 100, 40);
        public Color ToastActionBackground => new Color(215, 230, 200);
        public Color ToastActionBorder => new Color(80, 140, 70);
        public Color ToastGuildBackground => new Color(235, 225, 190);
        public Color ToastGuildBorder => new Color(170, 140, 50);

        // Grid tile - parchment tiles
        public Color GridTileBackground => new Color(225, 212, 185);
        public Color GridTileBorder => new Color(170, 145, 110);
        public Color GridTileHover => new Color(235, 222, 195);

        // Tab - warm tabs
        public Color TabActive => new Color(215, 200, 175);
        public Color TabInactive => new Color(200, 185, 160);
        public Color TabText => new Color(60, 40, 20);

        // Input - parchment input fields
        public Color InputBackground => new Color(250, 242, 225);
        public Color InputBorder => new Color(170, 145, 110);
        public Color InputText => new Color(50, 35, 20);
        public Color InputPlaceholder => new Color(160, 140, 110);

        // Tooltip - lighter parchment
        public Color TooltipBackground => new Color(250, 242, 225);
        public Color TooltipBorder => new Color(140, 110, 75);
        public Color TooltipText => new Color(50, 35, 20);

        // Slots - warm earthy slots
        public Color SlotBackground => new Color(50, 45, 40, 220);
        public Color SlotBackgroundAlt => new Color(55, 50, 45, 180);
        public Color SlotBorder => new Color(70, 60, 50);

        // List rows
        public Color ListRowEven => new Color(70, 60, 50, 150);
        public Color ListRowOdd => new Color(60, 50, 40, 150);
        public Color ListRowHover => new Color(255, 255, 255, 30);
        public Color ListHeaderBackground => new Color(60, 50, 40, 230);

        // Scrollbar
        public Color ScrollTrackBackground => new Color(40, 35, 30, 200);
        public Color ScrollTrackBorder => new Color(80, 70, 60);
        public Color ScrollThumbBorder => new Color(120, 110, 100);

        // Misc UI
        public Color ButtonDisabled => new Color(60, 55, 50);
        public Color SectionBackground => new Color(0, 0, 0, 60);
        public Color OverlayDim => new Color(0, 0, 0, 20);
        public Color BadgeBackground => new Color(0, 0, 0, 140);

        // Progress bar
        public Color ProgressBarBackground => new Color(40, 40, 50);
        public Color ProgressBarFill => new Color(80, 160, 220);

        // Semantic colors
        public Color CompletionColor => new Color(100, 200, 100);
        public Color AgreementColor => new Color(100, 255, 100);
        public Color DangerColor => new Color(80, 40, 40);
        public Color GoldColor => new Color(255, 215, 0);
        public Color LinkHoverColor => new Color(150, 230, 255);

        // Chat text colors (match original hardcoded values)
        public Color ChatDefault => Color.Black;
        public Color ChatServer => Color.FromNonPremultiplied(0x8a, 0x5c, 0x4a, 0xff);
        public Color ChatError => Color.FromNonPremultiplied(0x7d, 0x0a, 0x0a, 0xff);
        public Color ChatPM => Color.FromNonPremultiplied(0x5a, 0x3c, 0x00, 0xff);
        public Color ChatServerGlobal => Color.FromNonPremultiplied(0x8a, 0x6d, 0x00, 0xff);
        public Color ChatAdmin => Color.FromNonPremultiplied(0x7a, 0x4a, 0x2a, 0xff);
    }
}
