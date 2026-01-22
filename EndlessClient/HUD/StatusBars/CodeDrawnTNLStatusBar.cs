using System.Collections.Generic;
using EndlessClient.Content;
using EndlessClient.Input;
using EndlessClient.Rendering;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Graphics;
using Microsoft.Xna.Framework;

namespace EndlessClient.HUD.StatusBars
{
    public class CodeDrawnTNLStatusBar : CodeDrawnStatusBarBase
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IExperienceTableProvider _experienceTableProvider;

        protected override int StatusBarIndex => 1;
        protected override Color BarFillColor => _styleProvider.TNLBarFill;
        protected override string BarLabel => "TNL";

        public CodeDrawnTNLStatusBar(IClientWindowSizeProvider clientWindowSizeProvider,
                                     ICharacterProvider characterProvider,
                                     IUIStyleProvider styleProvider,
                                     IGraphicsDeviceProvider graphicsDeviceProvider,
                                     IContentProvider contentProvider,
                                     IExperienceTableProvider experienceTableProvider)
            : base(clientWindowSizeProvider, characterProvider, styleProvider, graphicsDeviceProvider, contentProvider)
        {
            _styleProvider = styleProvider;
            _experienceTableProvider = experienceTableProvider;
            DrawArea = new Rectangle(430, 0, DrawArea.Width, DrawArea.Height);
            ChangeStatusBarPosition();
        }

        protected override void UpdateLabelText()
        {
            _label.Text = $"{ExpTable[Stats[CharacterStat.Level] + 1] - Stats[CharacterStat.Experience]}";
        }

        protected override float GetFillPercentage()
        {
            var thisLevelExp = ExpTable[Stats[CharacterStat.Level]];
            var nextLevelExp = ExpTable[Stats[CharacterStat.Level] + 1];
            var expRange = nextLevelExp - thisLevelExp;
            if (expRange <= 0) return 1f;
            return (float)(Stats[CharacterStat.Experience] - thisLevelExp) / expRange;
        }

        private IReadOnlyList<int> ExpTable => _experienceTableProvider.ExperienceByLevel;
    }
}
