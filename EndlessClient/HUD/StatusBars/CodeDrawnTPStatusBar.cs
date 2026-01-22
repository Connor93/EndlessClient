using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Graphics;
using Microsoft.Xna.Framework;

namespace EndlessClient.HUD.StatusBars
{
    public class CodeDrawnTPStatusBar : CodeDrawnStatusBarBase
    {
        private readonly IUIStyleProvider _styleProvider;

        protected override int StatusBarIndex => -1;
        protected override Color BarFillColor => _styleProvider.TPBarFill;
        protected override string BarLabel => "TP";

        public CodeDrawnTPStatusBar(IClientWindowSizeProvider clientWindowSizeProvider,
                                    ICharacterProvider characterProvider,
                                    IUIStyleProvider styleProvider,
                                    IGraphicsDeviceProvider graphicsDeviceProvider,
                                    IContentProvider contentProvider)
            : base(clientWindowSizeProvider, characterProvider, styleProvider, graphicsDeviceProvider, contentProvider)
        {
            _styleProvider = styleProvider;
            DrawArea = new Rectangle(210, 0, DrawArea.Width, DrawArea.Height);
            ChangeStatusBarPosition();
        }

        protected override void UpdateLabelText()
        {
            _label.Text = $"{Stats[CharacterStat.TP]}/{Stats[CharacterStat.MaxTP]}";
        }

        protected override float GetFillPercentage()
        {
            var maxTP = Stats[CharacterStat.MaxTP];
            return maxTP > 0 ? (float)Stats[CharacterStat.TP] / maxTP : 0f;
        }
    }
}
