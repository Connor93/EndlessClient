using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Graphics;
using Microsoft.Xna.Framework;

namespace EndlessClient.HUD.StatusBars
{
    public class CodeDrawnHPStatusBar : CodeDrawnStatusBarBase
    {
        private readonly IUIStyleProvider _styleProvider;

        protected override int StatusBarIndex => -2;
        protected override Color BarFillColor => _styleProvider.HPBarFill;
        protected override string BarLabel => "HP";

        public CodeDrawnHPStatusBar(IClientWindowSizeProvider clientWindowSizeProvider,
                                    ICharacterProvider characterProvider,
                                    IUIStyleProvider styleProvider,
                                    IGraphicsDeviceProvider graphicsDeviceProvider,
                                    IContentProvider contentProvider)
            : base(clientWindowSizeProvider, characterProvider, styleProvider, graphicsDeviceProvider, contentProvider)
        {
            _styleProvider = styleProvider;
            DrawArea = new Rectangle(100, 0, DrawArea.Width, DrawArea.Height);
            ChangeStatusBarPosition();
        }

        protected override void UpdateLabelText()
        {
            _label.Text = $"{Stats[CharacterStat.HP]}/{Stats[CharacterStat.MaxHP]}";
        }

        protected override float GetFillPercentage()
        {
            var maxHP = Stats[CharacterStat.MaxHP];
            return maxHP > 0 ? (float)Stats[CharacterStat.HP] / maxHP : 0f;
        }
    }
}
