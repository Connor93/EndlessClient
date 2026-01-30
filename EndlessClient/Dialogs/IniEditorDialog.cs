using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Dialogs.Services;
using EndlessClient.HUD.Controls;
using EndlessClient.UIControls;
using EOLib.Domain.IniEditor;
using EOLib.Domain.Login;
using EOLib.Domain.Notifiers;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class IniEditorDialog : ScrollingListDialog, IIniEditorNotifier
    {
        private enum IniEditorState
        {
            FileList,
            EditFile,
        }

        private readonly IIniEditorActions _iniEditorActions;
        private readonly IIniEditorProvider _iniEditorProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IHudControlProvider _hudControlProvider;

        private readonly XNATextBox _contentEditor;

        private IniEditorState _state;
        private string _currentFilename;
        private int _currentDirType;
        private bool _contentModified;

        public IniEditorDialog(INativeGraphicsManager nativeGraphicsManager,
                               IEODialogButtonService dialogButtonService,
                               IIniEditorActions iniEditorActions,
                               IIniEditorProvider iniEditorProvider,
                               IEOMessageBoxFactory messageBoxFactory,
                               IContentProvider contentProvider,
                               IHudControlProvider hudControlProvider)
            : base(nativeGraphicsManager, dialogButtonService, DialogType.Shop)
        {
            _iniEditorActions = iniEditorActions;
            _iniEditorProvider = iniEditorProvider;
            _messageBoxFactory = messageBoxFactory;
            _hudControlProvider = hudControlProvider;

            ListItemType = ListDialogItem.ListItemStyle.Small;
            Title = "INI Editor";
            _state = IniEditorState.FileList;
            Buttons = ScrollingListDialogButtons.Cancel;

            _contentEditor = new ClearableTextBox(new Rectangle(18, 44, 430, 204), Constants.FontSize08, caretTexture: contentProvider.Textures[ContentProvider.Cursor])
            {
                TextAlignment = LabelAlignment.TopLeft,
                TextColor = ColorConstants.LightGrayText,
                Visible = false,
                MaxWidth = 400,
                Multiline = true,
                ScrollHandler = _scrollBar,
                RowSpacing = 14,
            };
            _contentEditor.SetScrollWheelHandler(_scrollBar);
            _contentEditor.SetParentControl(this);
            _contentEditor.OnTextChanged += (_, _) => _contentModified = true;

            // Request file list on open
            _iniEditorActions.RequestFileList();
        }

        public override void Initialize()
        {
            _contentEditor.Initialize();
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (_state == IniEditorState.FileList)
            {
                var allFiles = _iniEditorProvider.ConfigFiles.Concat(_iniEditorProvider.DataFiles).ToList();
                if (allFiles.Count > 0 && ChildControls.OfType<ListDialogItem>().Count() == 0)
                {
                    RefreshFileList();
                }
            }

            base.OnUpdateControl(gameTime);
        }

        private void RefreshFileList()
        {
            ClearItemList();

            var index = 0;

            // Config files header
            if (_iniEditorProvider.ConfigFiles.Count > 0)
            {
                var configHeader = new ListDialogItem(this, ListDialogItem.ListItemStyle.Small, index++)
                {
                    PrimaryText = "=== Config Files ===",
                    Visible = true,
                    UnderlineLinks = false,
                    OffsetX = 2,
                    OffsetY = 44,
                };
                configHeader.DrawArea = new Rectangle(configHeader.DrawArea.Location, new Point(427, 16));
                configHeader.SetScrollWheelHandler(this);
                AddItemToList(configHeader, sortList: false);

                foreach (var file in _iniEditorProvider.ConfigFiles)
                {
                    var item = new ListDialogItem(this, ListDialogItem.ListItemStyle.Small, index++)
                    {
                        PrimaryText = file,
                        Data = (0, file), // 0 = config dir
                        Visible = true,
                        UnderlineLinks = true,
                        OffsetX = 12,
                        OffsetY = 44,
                    };
                    item.DrawArea = new Rectangle(item.DrawArea.Location, new Point(427, 16));
                    item.SetPrimaryClickAction(FileItem_Click);
                    item.SetScrollWheelHandler(this);
                    AddItemToList(item, sortList: false);
                }
            }

            // Data files header
            if (_iniEditorProvider.DataFiles.Count > 0)
            {
                var dataHeader = new ListDialogItem(this, ListDialogItem.ListItemStyle.Small, index++)
                {
                    PrimaryText = "=== Data Files ===",
                    Visible = true,
                    UnderlineLinks = false,
                    OffsetX = 2,
                    OffsetY = 44,
                };
                dataHeader.DrawArea = new Rectangle(dataHeader.DrawArea.Location, new Point(427, 16));
                dataHeader.SetScrollWheelHandler(this);
                AddItemToList(dataHeader, sortList: false);

                foreach (var file in _iniEditorProvider.DataFiles)
                {
                    var item = new ListDialogItem(this, ListDialogItem.ListItemStyle.Small, index++)
                    {
                        PrimaryText = file,
                        Data = (1, file), // 1 = data dir
                        Visible = true,
                        UnderlineLinks = true,
                        OffsetX = 12,
                        OffsetY = 44,
                    };
                    item.DrawArea = new Rectangle(item.DrawArea.Location, new Point(427, 16));
                    item.SetPrimaryClickAction(FileItem_Click);
                    item.SetScrollWheelHandler(this);
                    AddItemToList(item, sortList: false);
                }
            }

            _scrollBar.ScrollToTop();
        }

        private void FileItem_Click(object sender, MouseEventArgs e)
        {
            // Get the data tuple from either the ListDialogItem directly
            // or from its parent if the click came from the XNAHyperLink child
            (int dirType, string filename)? fileData = sender switch
            {
                ListDialogItem item => item.Data as (int, string)?,
                XNAControls.IXNAHyperLink link when link.ImmediateParent is ListDialogItem parentItem
                    => parentItem.Data as (int, string)?,
                _ => null
            };

            if (fileData == null)
                return;

            var (dirType, filename) = fileData.Value;
            _currentDirType = dirType;
            _currentFilename = filename;
            _iniEditorActions.RequestFileContent(dirType, filename);
        }

        private void SetState(IniEditorState state)
        {
            if (_state == state)
                return;

            _state = state;

            switch (_state)
            {
                case IniEditorState.FileList:
                    _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox).Selected = true;

                    Buttons = ScrollingListDialogButtons.Cancel;
                    Title = "INI Editor";
                    _contentEditor.Visible = false;
                    _contentEditor.Selected = false;
                    _contentModified = false;

                    _scrollBar.DrawArea = new Rectangle(
                        _scrollBar.DrawArea.X, 44,
                        _scrollBar.DrawArea.Width, GetScrollBarHeight(DialogType));

                    RefreshFileList();
                    break;

                case IniEditorState.EditFile:
                    // Deselect chat textbox so it doesn't steal focus
                    _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox).Selected = false;

                    Buttons = ScrollingListDialogButtons.OkCancel;
                    Title = _currentFilename;

                    _contentEditor.TabOrder = 0;
                    _contentEditor.Visible = true;
                    _contentEditor.Enabled = true;
                    _contentEditor.Selected = true;
                    _contentModified = false;

                    _scrollBar.DrawArea = new Rectangle(
                        _scrollBar.DrawArea.X, 44,
                        _scrollBar.DrawArea.Width, GetScrollBarHeight(DialogType));

                    ClearItemList();
                    break;
            }
        }

        protected override void CloseButton_Click(object sender, MouseEventArgs e)
        {
            if (sender == _cancel && _state == IniEditorState.FileList)
            {
                Close(XNADialogResult.Cancel);
            }
            else if (sender == _cancel && _state == IniEditorState.EditFile)
            {
                if (_contentModified)
                {
                    var dlg = _messageBoxFactory.CreateMessageBox("Discard changes?", "Changes have been made. Discard them?", EODialogButtons.OkCancel);
                    dlg.DialogClosing += (_, e) =>
                    {
                        if (e.Result == XNADialogResult.OK)
                        {
                            SetState(IniEditorState.FileList);
                        }
                    };
                    dlg.ShowDialog();
                }
                else
                {
                    SetState(IniEditorState.FileList);
                }
            }
            else if (sender == _ok && _state == IniEditorState.EditFile)
            {
                // Save the file
                var dlg = _messageBoxFactory.CreateMessageBox("Save changes?", $"Save changes to {_currentFilename}?", EODialogButtons.OkCancel);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                    {
                        _iniEditorActions.SaveFileContent(_currentDirType, _currentFilename, _contentEditor.Text);
                    }
                };
                dlg.ShowDialog();
            }
        }

        // IIniEditorNotifier implementation
        public void NotifyIniFileListReceived(IReadOnlyList<string> configFiles, IReadOnlyList<string> dataFiles)
        {
            // FileList will be refreshed in OnUpdateControl
        }

        public void NotifyIniFileContentReceived(int dirType, string filename, string content)
        {
            _currentDirType = dirType;
            _currentFilename = filename;
            _contentEditor.Text = content;
            SetState(IniEditorState.EditFile);
        }

        public void NotifyIniFileSaveResult(bool success, string message)
        {
            if (success)
            {
                var dlg = _messageBoxFactory.CreateMessageBox("Save Successful", $"File saved successfully.\n\nNote: Run $rehash to apply changes.");
                dlg.ShowDialog();
                _contentModified = false;
                SetState(IniEditorState.FileList);
            }
            else
            {
                var dlg = _messageBoxFactory.CreateMessageBox("Save Failed", $"Failed to save file: {message}");
                dlg.ShowDialog();
            }
        }
    }
}
