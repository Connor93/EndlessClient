using System;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Interact.Quest;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Optional;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn version of QuestDialog that uses procedural rendering.
    /// </summary>
    public class CodeDrawnQuestDialog : CodeDrawnScrollingListDialog
    {
        private enum State
        {
            TalkToNpc,
            SwitchQuest
        }

        private readonly IQuestActions _questActions;
        private readonly IQuestDataProvider _questDataProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IContentProvider _contentProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;

        private Option<QuestDialogData> _cachedData;
        private int _pageIndex = 0;
        private State _state = State.TalkToNpc;

        private CodeDrawnButton _questSwitcher;
        private CodeDrawnButton _nextButton;
        private CodeDrawnButton _backButton;
        private CodeDrawnButton _cancelButton;
        private CodeDrawnButton _okButton;

        public event EventHandler ClickSoundEffect;

        public CodeDrawnQuestDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IQuestActions questActions,
            IQuestDataProvider questDataProvider,
            IENFFileProvider enfFileProvider,
            IContentProvider contentProvider,
            ILocalizedStringFinder localizedStringFinder)
            : base(styleProvider, gameStateProvider, contentProvider.Fonts[Constants.FontSize08pt5])
        {
            _questActions = questActions;
            _questDataProvider = questDataProvider;
            _enfFileProvider = enfFileProvider;
            _contentProvider = contentProvider;
            _localizedStringFinder = localizedStringFinder;

            _cachedData = Option.None<QuestDialogData>();

            // Configure for NpcQuestDialog size (smaller)
            DialogWidth = 290;
            DialogHeight = 200;
            ListAreaTop = 40;
            ListAreaHeight = 110;
            ItemHeight = 16;

            // Update scrollbar after dimension changes
            UpdateScrollBarLayout();

            CreateButtons(styleProvider);

            DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                    _questActions.RespondToQuestDialog(DialogReply.Ok);
            };
        }

        private void CreateButtons(IUIStyleProvider styleProvider)
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            var buttonWidth = 60;
            var buttonHeight = 24;
            var buttonY = DialogHeight - 32;

            // Quest switcher button (top right)
            _questSwitcher = new CodeDrawnButton(styleProvider, font)
            {
                Text = "≡",
                DrawArea = new Rectangle(DialogWidth - 30, 12, 20, 20),
                Visible = false
            };
            _questSwitcher.OnClick += (_, _) =>
            {
                ToggleSwitcherState();
                ClickSoundEffect?.Invoke(this, EventArgs.Empty);
            };
            _questSwitcher.SetParentControl(this);

            // Back button
            _backButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Back",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - 8, buttonY, buttonWidth, buttonHeight),
                Visible = false
            };
            _backButton.OnClick += PreviousPage;
            _backButton.SetParentControl(this);

            // Next button
            _nextButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Next",
                DrawArea = new Rectangle(DialogWidth / 2 + 8, buttonY, buttonWidth, buttonHeight),
                Visible = false
            };
            _nextButton.OnClick += NextPage;
            _nextButton.SetParentControl(this);

            // Cancel button
            _cancelButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "Cancel",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - 8, buttonY, buttonWidth, buttonHeight),
                Visible = false
            };
            _cancelButton.OnClick += (_, _) => Close(XNADialogResult.Cancel);
            _cancelButton.SetParentControl(this);

            // OK button
            _okButton = new CodeDrawnButton(styleProvider, font)
            {
                Text = "OK",
                DrawArea = new Rectangle(DialogWidth / 2 + 8, buttonY, buttonWidth, buttonHeight),
                Visible = false
            };
            _okButton.OnClick += (_, _) => Close(XNADialogResult.OK);
            _okButton.SetParentControl(this);

            CenterInGameView();
        }

        public override void Initialize()
        {
            _questSwitcher?.Initialize();
            _nextButton?.Initialize();
            _backButton?.Initialize();
            _cancelButton?.Initialize();
            _okButton?.Initialize();
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            _questDataProvider.QuestDialogData.MatchSome(data => UpdateCachedDataIfNeeded(_cachedData, data));
            base.OnUpdateControl(gameTime);
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
            UpdateButtons(repoData);
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

                        Title = titleText;
                    }
                    else
                    {
                        Title = string.Empty;
                    }
                    break;
                case State.SwitchQuest:
                    Title = _localizedStringFinder.GetString(EOResourceID.SELECT_A_QUEST);
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

                        foreach (var action in repoData.Actions)
                        {
                            var linkIndex = action.ActionID;
                            AddItem($"▸ {action.DisplayText}", onClick: _ =>
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
                            });
                        }
                    }
                    break;
            }
        }

        private void UpdateButtons(QuestDialogData repoData)
        {
            _backButton.Visible = false;
            _nextButton.Visible = false;
            _cancelButton.Visible = false;
            _okButton.Visible = false;

            switch (_state)
            {
                case State.TalkToNpc:
                    bool morePages = _pageIndex < repoData.PageText.Count - 1;
                    bool firstPage = _pageIndex == 0;

                    if (firstPage && morePages)
                    {
                        _cancelButton.Visible = true;
                        _nextButton.Visible = true;
                    }
                    else if (!firstPage && morePages)
                    {
                        _backButton.Visible = true;
                        _nextButton.Visible = true;
                    }
                    else if (firstPage)
                    {
                        _cancelButton.Visible = true;
                        _okButton.Visible = true;
                    }
                    else
                    {
                        _backButton.Visible = true;
                        _okButton.Visible = true;
                    }
                    break;
                case State.SwitchQuest:
                    _cancelButton.Visible = true;
                    _cancelButton.DrawArea = new Rectangle((DialogWidth - 60) / 2, DialogHeight - 32, 60, 24);
                    break;
            }
        }

        private void NextPage(object sender, EventArgs e)
        {
            _cachedData.MatchSome(data =>
            {
                _pageIndex++;
                UpdateDialogDisplayText(data);
                UpdateButtons(data);
            });
        }

        private void PreviousPage(object sender, EventArgs e)
        {
            _cachedData.MatchSome(data =>
            {
                _pageIndex--;
                UpdateDialogDisplayText(data);
                UpdateButtons(data);
            });
        }
    }
}
