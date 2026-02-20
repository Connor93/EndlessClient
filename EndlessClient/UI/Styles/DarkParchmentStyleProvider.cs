using AutomaticTypeMapper;
using EOLib.Config;
using Microsoft.Xna.Framework;

namespace EndlessClient.UI.Styles
{
    /// <summary>
    /// Dark Parchment style - dark variant of Parchment with warm earthy tones
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class DarkParchmentStyleProvider : IUIStyleProvider
    {
        // Panel/Dialog - dark warm brown
        public Color PanelBackground => new Color(35, 30, 25);
        public Color PanelBackgroundAlt => new Color(45, 38, 30);
        public Color PanelBorder => new Color(100, 75, 50);

        // Title bar - deep brown
        public Color TitleBarBackground => new Color(55, 45, 35);
        public Color TitleBarText => new Color(220, 205, 180);

        // Buttons - dark warm tones with brown borders
        public Color ButtonNormal => new Color(60, 50, 40);
        public Color ButtonHover => new Color(75, 62, 48);
        public Color ButtonPressed => new Color(45, 38, 30);
        public Color ButtonBorder => new Color(120, 95, 65);
        public Color ButtonText => new Color(215, 200, 175);

        // Text - light cream/tan tones
        public Color TextPrimary => new Color(220, 205, 180);
        public Color TextSecondary => new Color(170, 150, 120);
        public Color TextHighlight => new Color(220, 150, 70);

        // Status Bars - dark backgrounds, vivid fills
        public Color StatusBarBackground => new Color(30, 25, 20);
        public Color StatusBarBorder => new Color(100, 75, 50);
        public Color HPBarFill => new Color(190, 55, 55);
        public Color TPBarFill => new Color(75, 155, 65);
        public Color SPBarFill => new Color(65, 120, 185);
        public Color TNLBarFill => new Color(200, 165, 50);

        // Metrics
        public int CornerRadius => 3;
        public int BorderThickness => 2;
        public int TitleBarHeight => 26;
        public int ButtonPadding => 10;

        // Toast notifications - dark warm tones
        public Color ToastInfoBackground => new Color(40, 38, 35);
        public Color ToastInfoBorder => new Color(80, 100, 140);
        public Color ToastWarningBackground => new Color(50, 40, 30);
        public Color ToastWarningBorder => new Color(180, 100, 40);
        public Color ToastActionBackground => new Color(35, 45, 35);
        public Color ToastActionBorder => new Color(80, 140, 70);
        public Color ToastGuildBackground => new Color(45, 42, 32);
        public Color ToastGuildBorder => new Color(170, 140, 50);

        // Grid tile - dark parchment tiles
        public Color GridTileBackground => new Color(45, 38, 30);
        public Color GridTileBorder => new Color(85, 70, 50);
        public Color GridTileHover => new Color(55, 48, 38);

        // Tab - dark warm tabs
        public Color TabActive => new Color(60, 50, 40);
        public Color TabInactive => new Color(40, 35, 28);
        public Color TabText => new Color(215, 200, 175);

        // Input - very dark brown
        public Color InputBackground => new Color(28, 24, 20);
        public Color InputBorder => new Color(85, 70, 50);
        public Color InputText => new Color(220, 205, 180);
        public Color InputPlaceholder => new Color(110, 90, 65);

        // Tooltip - dark warm
        public Color TooltipBackground => new Color(40, 35, 28);
        public Color TooltipBorder => new Color(100, 75, 50);
        public Color TooltipText => new Color(220, 205, 180);

        // Slots - deep dark warm
        public Color SlotBackground => new Color(30, 26, 22, 220);
        public Color SlotBackgroundAlt => new Color(35, 30, 25, 180);
        public Color SlotBorder => new Color(65, 50, 38);

        // List rows
        public Color ListRowEven => new Color(45, 38, 30, 150);
        public Color ListRowOdd => new Color(38, 32, 25, 150);
        public Color ListRowHover => new Color(255, 220, 180, 25);
        public Color ListHeaderBackground => new Color(55, 45, 35, 230);

        // Scrollbar
        public Color ScrollTrackBackground => new Color(28, 24, 20, 200);
        public Color ScrollTrackBorder => new Color(65, 50, 38);
        public Color ScrollThumbBorder => new Color(100, 80, 58);

        // Misc UI
        public Color ButtonDisabled => new Color(50, 42, 35);
        public Color SectionBackground => new Color(0, 0, 0, 80);
        public Color OverlayDim => new Color(0, 0, 0, 30);
        public Color BadgeBackground => new Color(0, 0, 0, 160);

        // Progress bar
        public Color ProgressBarBackground => new Color(28, 24, 20);
        public Color ProgressBarFill => new Color(70, 140, 200);

        // Semantic colors
        public Color CompletionColor => new Color(90, 190, 90);
        public Color AgreementColor => new Color(90, 230, 90);
        public Color DangerColor => new Color(160, 60, 60);
        public Color GoldColor => new Color(240, 200, 50);
        public Color LinkHoverColor => new Color(200, 175, 120);

        // Chat text colors (lighter variants for dark background)
        public Color ChatDefault => new Color(210, 195, 175);
        public Color ChatServer => new Color(200, 170, 140);
        public Color ChatError => new Color(220, 90, 90);
        public Color ChatPM => new Color(210, 180, 100);
        public Color ChatServerGlobal => new Color(220, 200, 80);
        public Color ChatAdmin => new Color(200, 160, 110);
    }
}
