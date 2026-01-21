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

        // Metrics
        public int CornerRadius => 4;
        public int BorderThickness => 1;
        public int TitleBarHeight => 26;
        public int ButtonPadding => 10;
    }
}
