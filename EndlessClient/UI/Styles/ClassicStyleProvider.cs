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
    }
}
