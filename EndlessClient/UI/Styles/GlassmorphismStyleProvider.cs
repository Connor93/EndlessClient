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

        // Buttons
        public Color ButtonNormal => new Color(60, 70, 100, 180);
        public Color ButtonHover => new Color(80, 95, 130, 200);
        public Color ButtonPressed => new Color(40, 50, 70, 220);
        public Color ButtonBorder => new Color(90, 110, 140, 180);
        public Color ButtonText => new Color(220, 225, 235);

        // Text
        public Color TextPrimary => new Color(230, 235, 245);
        public Color TextSecondary => new Color(160, 170, 190);

        // Metrics
        public int CornerRadius => 6;
        public int BorderThickness => 1;
        public int TitleBarHeight => 28;
        public int ButtonPadding => 12;
    }
}
