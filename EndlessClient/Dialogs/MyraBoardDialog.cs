using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Board;
using EOLib.Domain.Login;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraBoardDialog : MyraScrollingListDialog
    {
        private enum BoardDialogState
        {
            ViewList,
            ViewPost,
            CreatePost,
        }

        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IBoardActions _boardActions;
        private readonly IBoardRepository _boardRepository;
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly ICharacterProvider _characterProvider;

        private BoardDialogState _state;
        private HashSet<BoardPostInfo> _cachedPostInfo;

        // Text input widgets for compose/view states
        private TextBox _subjectTextBox;
        private TextBox _messageTextBox;
        private VerticalStackPanel _editorPanel;

        public MyraBoardDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            IEOMessageBoxFactory eoMessageBoxFactory,
            IBoardActions boardActions,
            IBoardRepository boardRepository,
            IPlayerInfoProvider playerInfoProvider,
            ICharacterProvider characterProvider)
            : base(uiManager, fontProvider, localizedStringFinder.GetString(EOResourceID.BOARD_TOWN_BOARD), width: 320, height: 280, showAdd: true)
        {
            _localizedStringFinder = localizedStringFinder;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _boardActions = boardActions;
            _boardRepository = boardRepository;
            _playerInfoProvider = playerInfoProvider;
            _characterProvider = characterProvider;

            _state = BoardDialogState.ViewList;
            _cachedPostInfo = new HashSet<BoardPostInfo>();

            CancelClosesDialog = false;
            OkClosesDialog = false;

            AddAction += (_, _) => AddButton_Click();
            CancelAction += CancelButton_Click;
            OkAction += OkButton_Click;
            DeleteAction += DeleteButton_Click;

            BuildEditorPanel(fontProvider);
        }

        private void BuildEditorPanel(IMyraFontProvider fontProvider)
        {
            _subjectTextBox = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 24,
            };

            _messageTextBox = new TextBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Multiline = true,
                Height = 140,
                Wrap = true,
            };

            var subjectLabel = new Label { Text = "Subject:" };

            _editorPanel = new VerticalStackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visible = false,
            };
            _editorPanel.Widgets.Add(subjectLabel);
            _editorPanel.Widgets.Add(_subjectTextBox);
            _editorPanel.Widgets.Add(_messageTextBox);
        }

        public override void Update(GameTime gameTime)
        {
            switch (_state)
            {
                case BoardDialogState.ViewList:
                    if (!_cachedPostInfo.SetEquals(_boardRepository.Posts))
                    {
                        ClearItems();

                        _cachedPostInfo = new HashSet<BoardPostInfo>(_boardRepository.Posts);

                        foreach (var post in _cachedPostInfo)
                        {
                            var postId = post.PostId;
                            AddItem(
                                char.ToUpper(post.Author[0]) + post.Author[1..],
                                subText: post.Subject,
                                onClick: _ => ChildItem_Click(postId),
                                isLink: true,
                                data: postId);
                        }
                    }
                    break;

                case BoardDialogState.ViewPost:
                    if (_boardRepository.ActivePostMessage.Map(msg => msg != _messageTextBox.Text).ValueOr(false))
                    {
                        _boardRepository.ActivePostMessage.MatchSome(msg => _messageTextBox.Text = msg);
                    }
                    break;
            }

            base.Update(gameTime);
        }

        private void SetState(BoardDialogState state, int postId = -1)
        {
            if (state == _state)
                return;

            _state = state;

            switch (_state)
            {
                case BoardDialogState.ViewList:
                    SetupButtons(showOk: false, showCancel: true, showBack: false, showAdd: true);
                    SetTitle(_localizedStringFinder.GetString(EOResourceID.BOARD_TOWN_BOARD));
                    _editorPanel.Visible = false;
                    ShowListPanel(true);
                    _cachedPostInfo.Clear();
                    break;

                case BoardDialogState.CreatePost:
                    SetupButtons(showOk: true, showCancel: true, showBack: false);
                    ShowAddButton(false);
                    SetTitle(_localizedStringFinder.GetString(EOResourceID.BOARD_POSTING_NEW_MESSAGE));
                    _subjectTextBox.Text = string.Empty;
                    _messageTextBox.Text = string.Empty;
                    _subjectTextBox.Readonly = false;
                    _messageTextBox.Readonly = false;
                    ShowListPanel(false);
                    InsertEditorPanel();
                    break;

                case BoardDialogState.ViewPost:
                    var author = _boardRepository.ActivePost.Map(x => x.Author).ValueOr(string.Empty);
                    var matchesAuthor = author.IndexOf(_characterProvider.MainCharacter.Name, StringComparison.OrdinalIgnoreCase) >= 0;
                    var canDelete = _playerInfoProvider.PlayerHasAdminCharacter || matchesAuthor;

                    SetupButtons(showOk: false, showCancel: true, showBack: false, showDelete: canDelete);
                    ShowAddButton(false);

                    _boardRepository.Posts.SingleOrNone(x => x.PostId == postId)
                        .MatchSome(post =>
                        {
                            SetTitle(post.Author);
                            _subjectTextBox.Text = post.Subject;
                        });

                    _messageTextBox.Text = _localizedStringFinder.GetString(EOResourceID.BOARD_LOADING_MESSAGE);
                    _subjectTextBox.Readonly = true;
                    _messageTextBox.Readonly = true;
                    ShowListPanel(false);
                    InsertEditorPanel();
                    break;
            }
        }

        private void InsertEditorPanel()
        {
            _editorPanel.Visible = true;
            if (!ContentPanel.Widgets.Contains(_editorPanel))
            {
                ContentPanel.Widgets.Add(_editorPanel);
            }
        }

        private void AddButton_Click()
        {
            var numPostsByThisPlayer = _boardRepository.Posts.Count(x => string.Equals(x.Author, _characterProvider.MainCharacter.Name, StringComparison.OrdinalIgnoreCase));
            if (numPostsByThisPlayer > 2)
            {
                var dlg = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.BOARD_ERROR_TOO_MANY_MESSAGES);
                dlg.ShowDialog();
            }
            else
            {
                SetState(BoardDialogState.CreatePost);
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (_state == BoardDialogState.CreatePost)
            {
                if (string.IsNullOrEmpty(_messageTextBox.Text))
                {
                    var dlg = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.BOARD_ERROR_NO_MESSAGE);
                    dlg.ShowDialog();
                    return;
                }
                else if (string.IsNullOrEmpty(_subjectTextBox.Text))
                {
                    var dlg = _eoMessageBoxFactory.CreateMessageBox(DialogResourceID.BOARD_ERROR_NO_SUBJECT);
                    dlg.ShowDialog();
                    return;
                }

                _boardActions.AddPost(_subjectTextBox.Text, _messageTextBox.Text);
            }

            _boardRepository.ActivePost = Option.None<BoardPostInfo>();
            SetState(BoardDialogState.ViewList);
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            _boardRepository.ActivePost.MatchSome(x => _boardActions.DeletePost(x.PostId));
            _boardRepository.ActivePost = Option.None<BoardPostInfo>();
            SetState(BoardDialogState.ViewList);
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (_state == BoardDialogState.ViewList)
            {
                Close(XNADialogResult.Cancel);
            }
            else
            {
                _boardRepository.ActivePost = Option.None<BoardPostInfo>();
                SetState(BoardDialogState.ViewList);
            }
        }

        private void ChildItem_Click(int postId)
        {
            if (postId >= 0)
            {
                _boardRepository.ActivePost = _boardRepository.Posts.SingleOrNone(x => x.PostId == postId);
                SetState(BoardDialogState.ViewPost, postId);
                _boardActions.ViewPost(postId);
            }
        }
    }
}
