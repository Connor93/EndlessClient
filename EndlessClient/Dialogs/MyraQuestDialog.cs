using System;
using System.Linq;
using EndlessClient.UI.Myra;
using EOLib.Domain.Interact.Quest;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Myra.Graphics2D.UI;
using Optional;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based quest dialog — replaces CodeDrawnQuestDialog.
    /// Supports multi-page text, clickable quest actions, and quest switching.
    /// </summary>
    public class MyraQuestDialog : MyraScrollingListDialog
    {
        private enum State
        {
            TalkToNpc,
            SwitchQuest
        }

        private readonly IQuestActions _questActions;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;

        private Option<QuestDialogData> _cachedData;
        private int _pageIndex;
        private State _state = State.TalkToNpc;

        private Button _questSwitcher;

        public event EventHandler ClickSoundEffect;

        public MyraQuestDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IQuestActions questActions,
            IQuestDataProvider questDataProvider,
            IENFFileProvider enfFileProvider,
            ILocalizedStringFinder localizedStringFinder)
            : base(uiManager, fontProvider, string.Empty, width: 350, height: 300)
        {
            _questActions = questActions;
            _questDataProvider = questDataProvider;
            _enfFileProvider = enfFileProvider;
            _localizedStringFinder = localizedStringFinder;

            _cachedData = Option.None<QuestDialogData>();
            _pageIndex = 0;

            // Add quest switcher button to the window title bar area
            _questSwitcher = new Button
            {
                Content = new Label { Text = "≡", Font = fontProvider.Normal },
                Width = 24,
                Height = 20,
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            _questSwitcher.Click += (_, _) =>
            {
                ToggleSwitcherState();
                ClickSoundEffect?.Invoke(this, EventArgs.Empty);
            };

            // Add switcher to window content area (top of layout)
            if (Window.Content is VerticalStackPanel mainPanel)
            {
                var topBar = new HorizontalStackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Height = 22
                };
                topBar.Widgets.Add(_questSwitcher);
                mainPanel.Widgets.Insert(0, topBar);

                // Fix proportions after inserting topBar at index 0
                // Widget order is now: topBar(0), scrollViewer(1), buttonBar(2)
                mainPanel.Proportions.Clear();
                mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));  // topBar
                mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));  // scrollViewer
                mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));  // buttonBar
            }

            // Close handler for OK
            DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                    _questActions.RespondToQuestDialog(DialogReply.Ok);
            };

            // Back button navigates to previous page
            BackAction += (_, _) =>
            {
                _cachedData.MatchSome(data =>
                {
                    _pageIndex--;
                    UpdateDialogDisplayText(data);
                    UpdateButtonsForState(data);
                });
            };

            // Poll quest data on each render frame
            Window.BeforeRender += _ => PollQuestData();
        }

        private void PollQuestData()
        {
            _questDataProvider.QuestDialogData.MatchSome(data => UpdateCachedDataIfNeeded(_cachedData, data));
        }

        private void ToggleSwitcherState()
        {
            _state = _state == State.TalkToNpc ? State.SwitchQuest : State.TalkToNpc;
            _cachedData.MatchSome(UpdateDialogControls);
        }

        private void UpdateCachedDataIfNeeded(Option<QuestDialogData> cachedData, QuestDialogData repoData)
        {
            cachedData.Match(
                some: cached =>
                {
                    _cachedData = Option.Some(repoData);
                    if (!cached.Equals(repoData))
                    {
                        if (_state == State.SwitchQuest)
                            ToggleSwitcherState();

                        UpdateDialogControls(repoData);
                    }
                },
                none: () =>
                {
                    _cachedData = Option.Some(repoData);
                    UpdateDialogControls(repoData);
                });
        }

        private void UpdateDialogControls(QuestDialogData repoData)
        {
            _pageIndex = 0;

            UpdateTitle(repoData);
            UpdateQuestSwitcherButton(repoData);
            UpdateDialogDisplayText(repoData);
            UpdateButtonsForState(repoData);
        }

        private void UpdateTitle(QuestDialogData repoData)
        {
            switch (_state)
            {
                case State.TalkToNpc:
                    if (_questDataProvider.RequestedNPC != null)
                    {
                        var npcName = _enfFileProvider.ENFFile[_questDataProvider.RequestedNPC.ID].Name;
                        var titleText = npcName;
                        if (!repoData.DialogTitles.ContainsKey(repoData.VendorID) && repoData.DialogTitles.Count == 1)
                            titleText += $" - {repoData.DialogTitles.Single().Value}";
                        else if (repoData.DialogTitles.ContainsKey(repoData.VendorID))
                            titleText += $" - {repoData.DialogTitles[repoData.VendorID]}";

                        Window.Title = titleText;
                    }
                    else
                    {
                        Window.Title = string.Empty;
                    }
                    break;
                case State.SwitchQuest:
                    Window.Title = _localizedStringFinder.GetString(EOResourceID.SELECT_A_QUEST);
                    break;
            }
        }

        private void UpdateQuestSwitcherButton(QuestDialogData repoData)
        {
            _questSwitcher.Visible = repoData.DialogTitles.Count > 1;
        }

        private void UpdateDialogDisplayText(QuestDialogData repoData)
        {
            ClearItems();

            switch (_state)
            {
                case State.TalkToNpc:
                    {
                        var text = repoData.PageText[_pageIndex].Replace("\n", string.Empty);
                        AddItem(text);

                        // Links only shown on last page
                        if (_pageIndex < repoData.PageText.Count - 1)
                            return;

                        if (repoData.Actions.Count > 0)
                        {
                            // Add visual separator between text and actions
                            AddSeparator();
                        }

                        foreach (var action in repoData.Actions)
                        {
                            var linkIndex = action.ActionID;
                            AddItem($"> {action.DisplayText}", onClick: _ =>
                            {
                                _questActions.RespondToQuestDialog(DialogReply.Link, linkIndex);
                                ClickSoundEffect?.Invoke(this, EventArgs.Empty);
                                Close(XNADialogResult.Cancel);
                            }, isLink: true);
                        }
                    }
                    break;
                case State.SwitchQuest:
                    {
                        foreach (var title in repoData.DialogTitles)
                        {
                            var questId = title.Key;
                            AddItem(title.Value, onClick: _ =>
                            {
                                _questActions.RequestQuest(_questDataProvider.RequestedNPC.Index, questId);
                                ClickSoundEffect?.Invoke(this, EventArgs.Empty);
                            }, isLink: true);
                        }
                    }
                    break;
            }
        }

        private void UpdateButtonsForState(QuestDialogData repoData)
        {
            switch (_state)
            {
                case State.TalkToNpc:
                    bool morePages = _pageIndex < repoData.PageText.Count - 1;
                    bool firstPage = _pageIndex == 0;

                    if (firstPage && morePages)
                    {
                        SetupButtons(showOk: false, showCancel: true);
                        AddNextButton();
                    }
                    else if (!firstPage && morePages)
                    {
                        SetupButtons(showOk: false, showCancel: false, showBack: true);
                        AddNextButton();
                    }
                    else if (firstPage)
                    {
                        SetupButtons(showOk: true, showCancel: true);
                    }
                    else
                    {
                        SetupButtons(showOk: true, showCancel: false, showBack: true);
                    }
                    break;
                case State.SwitchQuest:
                    SetupButtons(showOk: false, showCancel: true);
                    break;
            }
        }

        private void AddNextButton()
        {
            // Add a "Next" button that advances the page
            // We add it via BackAction since the button bar is already set up
            // Actually, we need a custom button for Next
            var nextButton = new Button
            {
                Content = new Label { Text = "Next" },
                Width = 72,
                Height = 28
            };
            nextButton.Click += (_, _) =>
            {
                _cachedData.MatchSome(data =>
                {
                    _pageIndex++;
                    UpdateDialogDisplayText(data);
                    UpdateButtonsForState(data);
                });
            };

            // Access the button bar from the base class window
            if (Window.Content is VerticalStackPanel mainPanel)
            {
                // Find the button bar (last HorizontalStackPanel)
                for (int i = mainPanel.Widgets.Count - 1; i >= 0; i--)
                {
                    if (mainPanel.Widgets[i] is HorizontalStackPanel buttonBar)
                    {
                        buttonBar.Widgets.Add(nextButton);
                        break;
                    }
                }
            }
        }
    }
}
