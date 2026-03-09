using System.Collections.Generic;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Quest;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraQuestStatusDialog : MyraScrollingListDialog
    {
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly ICharacterProvider _characterProvider;

        private IReadOnlyList<QuestProgressData> _cachedProgress = new List<QuestProgressData>();
        private IReadOnlyList<string> _cachedHistory = new List<string>();

        private QuestPage _page;

        public MyraQuestStatusDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            IQuestDataProvider questDataProvider,
            ICharacterProvider characterProvider)
            : base(uiManager, fontProvider, string.Empty, width: 340, height: 300)
        {
            _localizedStringFinder = localizedStringFinder;
            _questDataProvider = questDataProvider;
            _characterProvider = characterProvider;

            _page = QuestPage.Progress;
            SetupButtons(showOk: true, showCancel: false, showBack: false, showHistory: true);

            HistoryAction += (_, _) => ShowHistory();
            ProgressAction += (_, _) => ShowProgress();

            SetTitle(QuestPage.Progress);
        }

        public override void Update(GameTime gameTime)
        {
            if (_questDataProvider.QuestHistory != _cachedHistory)
            {
                _cachedHistory = _questDataProvider.QuestHistory;
                if (_page == QuestPage.History)
                    ShowHistory();
            }
            else if (_questDataProvider.QuestProgress != _cachedProgress)
            {
                _cachedProgress = _questDataProvider.QuestProgress;
                if (_page == QuestPage.Progress)
                    ShowProgress();
            }

            base.Update(gameTime);
        }

        private void ShowHistory()
        {
            ClearItems();
            SetTitle(QuestPage.History);

            if (_cachedHistory.Count == 0)
            {
                AddItem(_localizedStringFinder.GetString(EOResourceID.QUEST_DID_NOT_FINISH_ANY));
            }

            foreach (var questName in _cachedHistory)
            {
                AddItem(questName, subText: _localizedStringFinder.GetString(EOResourceID.QUEST_COMPLETED));
            }

            _page = QuestPage.History;
            SetupButtons(showOk: true, showCancel: false, showBack: false, showProgress: true);
        }

        private void ShowProgress()
        {
            ClearItems();
            SetTitle(QuestPage.Progress);

            if (_cachedProgress.Count == 0)
            {
                AddItem(_localizedStringFinder.GetString(EOResourceID.QUEST_DID_NOT_START_ANY));
            }

            foreach (var quest in _cachedProgress)
            {
                var progress = quest.Target > 0 ? $"{quest.Progress} / {quest.Target}" : "n / a";
                AddItem(quest.Name, subText: $"{quest.Description}  [{progress}]");
            }

            _page = QuestPage.Progress;
            SetupButtons(showOk: true, showCancel: false, showBack: false, showHistory: true);
        }

        private void SetTitle(QuestPage page)
        {
            var resource = page == QuestPage.Progress
                ? EOResourceID.QUEST_PROGRESS
                : EOResourceID.QUEST_HISTORY;
            var description = _localizedStringFinder.GetString(resource);

            SetTitle($"{_characterProvider.MainCharacter.Name}'s {description}");
        }
    }
}
