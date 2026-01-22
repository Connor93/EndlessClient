using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Graphics;
using Microsoft.Xna.Framework;

namespace EndlessClient.HUD.StatusBars
{
    public class CodeDrawnSPStatusBar : CodeDrawnStatusBarBase
    {
        private readonly IUIStyleProvider _styleProvider;

        protected override int StatusBarIndex => 0;
        protected override Color BarFillColor => _styleProvider.SPBarFill;
        protected override string BarLabel => "SP";

        public CodeDrawnSPStatusBar(IClientWindowSizeProvider clientWindowSizeProvider,
                                    ICharacterProvider characterProvider,
                                    IUIStyleProvider styleProvider,
                                    IGraphicsDeviceProvider graphicsDeviceProvider,
                                    IContentProvider contentProvider)
            : base(clientWindowSizeProvider, characterProvider, styleProvider, graphicsDeviceProvider, contentProvider)
        {
            _styleProvider = styleProvider;
            DrawArea = new Rectangle(320, 0, DrawArea.Width, DrawArea.Height);
            ChangeStatusBarPosition();
        }

        protected override void UpdateLabelText()
        {
            _label.Text = $"{Stats[CharacterStat.SP]}/{Stats[CharacterStat.MaxSP]}";
        }

        protected override float GetFillPercentage()
        {
            var maxSP = Stats[CharacterStat.MaxSP];
            return maxSP > 0 ? (float)Stats[CharacterStat.SP] / maxSP : 0f;
        }
    }
}
