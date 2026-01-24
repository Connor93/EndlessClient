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
    }
}
