using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD.Controls;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EndlessClient.UIControls;
using EOLib.Domain.IniEditor;
using EOLib.Domain.Notifiers;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class IniEditorDialog : CodeDrawnScrollingListDialog, IIniEditorNotifier
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
        private readonly IContentProvider _contentProvider;

        private readonly XNATextBox _contentEditor;

        private IniEditorState _state;
        private string _currentFilename;
        private int _currentDirType;
        private bool _contentModified;

        // Editor content for post-scale drawing
        private string _editorContent = string.Empty;
        private int _editorScrollOffset;
        private readonly Texture2D _caretTexture;
        private double _caretBlinkTimer;

        public IniEditorDialog(IUIStyleProvider styleProvider,
                               IGameStateProvider gameStateProvider,
                               IClientWindowSizeProvider clientWindowSizeProvider,
                               IGraphicsDeviceProvider graphicsDeviceProvider,
                               IIniEditorActions iniEditorActions,
                               IIniEditorProvider iniEditorProvider,
                               IEOMessageBoxFactory messageBoxFactory,
                               IContentProvider contentProvider,
                               IHudControlProvider hudControlProvider,
                               BitmapFont font,
                               BitmapFont scaledFont)
            : base(styleProvider, gameStateProvider, clientWindowSizeProvider, graphicsDeviceProvider, contentProvider, font)
        {
            _iniEditorActions = iniEditorActions;
            _iniEditorProvider = iniEditorProvider;
            _messageBoxFactory = messageBoxFactory;
            _hudControlProvider = hudControlProvider;
            _contentProvider = contentProvider;

            Title = "INI Editor";
            _state = IniEditorState.FileList;
            DialogWidth = 450;
            DialogHeight = 320;
            ListAreaTop = 45;
            ListAreaHeight = 230;
            ItemHeight = 18;
            UpdateScrollBarLayout();

            SetupButtons(showOk: false, showCancel: true, showBack: false);

            // Store caret texture for post-scale drawing
            _caretTexture = contentProvider.Textures[ContentProvider.Cursor];

            // Create the content editor textbox (text and caret invisible - we draw them post-scale for crispness)
            _contentEditor = new ClearableTextBox(new Rectangle(18, 44, DialogWidth - 56, ListAreaHeight), Constants.FontSize08)
            {
                TextAlignment = LabelAlignment.TopLeft,
                TextColor = Color.Transparent, // Invisible text - we draw post-scale
                Visible = false,
                MaxWidth = DialogWidth - 70,
                HardBreak = DialogWidth - 70,
                Multiline = true,
                RowSpacing = 14,
            };
            _contentEditor.SetParentControl(this);
            _contentEditor.OnTextChanged += (_, _) =>
            {
                _contentModified = true;
                _editorContent = _contentEditor.Text;
            };

            // Override button click events
            OkAction += OnOkClick;
            CancelAction += OnCancelClick;

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
                if (allFiles.Count > 0 && NamesList.Count == 0)
                {
                    RefreshFileList();
                }
            }

            // Track scroll offset and blink timer for editor post-scale rendering
            if (_state == IniEditorState.EditFile)
            {
                if (_contentEditor.ScrollHandler != null)
                    _editorScrollOffset = _contentEditor.ScrollHandler.ScrollOffset;

                _caretBlinkTimer = gameTime.TotalGameTime.TotalMilliseconds;
            }

            base.OnUpdateControl(gameTime);
        }

        private void RefreshFileList()
        {
            ClearItems();

            // Config files header
            if (_iniEditorProvider.ConfigFiles.Count > 0)
            {
                AddItem("=== Config Files ===", isLink: false);

                foreach (var file in _iniEditorProvider.ConfigFiles)
                {
                    AddItem(file, data: (0, file), isLink: true, onClick: item => FileItem_Click(item));
                }
            }

            // Data files header
            if (_iniEditorProvider.DataFiles.Count > 0)
            {
                AddItem("=== Data Files ===", isLink: false);

                foreach (var file in _iniEditorProvider.DataFiles)
                {
                    AddItem(file, data: (1, file), isLink: true, onClick: item => FileItem_Click(item));
                }
            }
        }

        private void FileItem_Click(CodeDrawnListItem item)
        {
            if (item.Data is not (int dirType, string filename))
                return;

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

                    Title = "INI Editor";
                    _contentEditor.Visible = false;
                    _contentEditor.Selected = false;
                    _contentModified = false;
                    _editorContent = string.Empty;

                    // Reset buttons for file list - cancel should close dialog
                    CancelClosesDialog = true;
                    SetupButtons(showOk: false, showCancel: true, showBack: false);

                    // Reset scrollbar for list items (ItemHeight)
                    ScrollHandler.LinesToRender = ItemsToShow;
                    ScrollHandler.ScrollToTop();

                    RefreshFileList();
                    break;

                case IniEditorState.EditFile:
                    // Deselect chat textbox so it doesn't steal focus
                    _hudControlProvider.GetComponent<ChatTextBox>(HudControlIdentifier.ChatTextBox).Selected = false;

                    Title = _currentFilename;

                    _contentEditor.TabOrder = 0;
                    _contentEditor.Visible = true;
                    _contentEditor.Enabled = true;
                    _contentEditor.Selected = true;
                    _contentModified = false;

                    // Wire content editor to use dialog's scrollbar for scrolling
                    _contentEditor.ScrollHandler = ScrollHandler;

                    // Configure scrollbar for editor lines (14px line height)
                    ScrollHandler.LinesToRender = ListAreaHeight / 14;
                    ScrollHandler.ScrollToTop();

                    // Change to OK/Cancel for editing - cancel should go back to file list, not close
                    CancelClosesDialog = false;
                    SetupButtons(showOk: true, showCancel: true, showBack: false);

                    ClearItems();
                    break;
            }
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            if (_state == IniEditorState.FileList)
            {
                // Close dialog - already handled by base class
            }
            else if (_state == IniEditorState.EditFile)
            {
                if (_contentModified)
                {
                    // Temporarily suppress post-scale rendering so message box appears on top
                    SuppressPostScaleRendering = true;

                    var dlg = _messageBoxFactory.CreateMessageBox("Discard changes?", "Changes have been made. Discard them?", EODialogButtons.OkCancel);
                    dlg.DialogClosing += (_, e) =>
                    {
                        SuppressPostScaleRendering = false;
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
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            if (_state == IniEditorState.EditFile)
            {
                // Temporarily suppress post-scale rendering so message box appears on top
                SuppressPostScaleRendering = true;

                // Save the file
                var dlg = _messageBoxFactory.CreateMessageBox("Save changes?", $"Save changes to {_currentFilename}?", EODialogButtons.OkCancel);
                dlg.DialogClosing += (_, closingArgs) =>
                {
                    SuppressPostScaleRendering = false;
                    if (closingArgs.Result == XNADialogResult.OK)
                    {
                        _iniEditorActions.SaveFileContent(_currentDirType, _currentFilename, _contentEditor.Text);
                    }
                };
                dlg.ShowDialog();
            }
        }

        /// <summary>
        /// Override DrawBordersAndText to also draw editor content post-scale
        /// </summary>
        protected override void DrawBordersAndText(Vector2 scaledPos, float scale)
        {
            // Draw base dialog borders and text (title, list items)
            base.DrawBordersAndText(scaledPos, scale);

            // Draw editor content post-scale for crisp text
            if (_state == IniEditorState.EditFile && !string.IsNullOrEmpty(_editorContent))
            {
                DrawEditorContentPostScale(scaledPos, scale);
            }
        }

        /// <summary>
        /// Override to draw editor content in pre-scale mode (for message box z-ordering fallback)
        /// </summary>
        protected override void DrawCustomContentComplete(Rectangle drawPos)
        {
            // Draw editor content if in edit mode
            if (_state == IniEditorState.EditFile && !string.IsNullOrEmpty(_editorContent))
            {
                DrawEditorContentPreScale();
            }
        }

        /// <summary>
        /// Draws editor content in pre-scale mode (at 1:1 scale within the render target)
        /// </summary>
        private void DrawEditorContentPreScale()
        {
            var font = Font;
            var editorX = 12;
            var editorY = ListAreaTop;
            var editorWidth = DialogWidth - 48;
            var editorHeight = ListAreaHeight;
            var lineHeight = 14;

            // Word wrap the content to fit the editor width
            var lines = WrapText(_editorContent, font, editorWidth);

            // Draw each visible line
            var startLine = Math.Max(0, _editorScrollOffset);
            var linesPerPage = editorHeight / lineHeight;
            var endLine = Math.Min(lines.Count, startLine + linesPerPage);

            for (var i = startLine; i < endLine; i++)
            {
                var lineY = editorY + (i - startLine) * lineHeight;
                _spriteBatch.DrawString(font, lines[i], new Vector2(editorX, lineY), StyleProvider.TextPrimary);
            }

            // Draw blinking caret if textbox is selected
            if (_contentEditor.Selected && _caretTexture != null)
            {
                // Blink every 500ms (same as XNATextBox)
                var showCaret = !(_caretBlinkTimer % 1000 < 500);
                if (showCaret)
                {
                    // Calculate cursor row/col based on OUR wrapping (not textbox's)
                    var cursorPos = _contentEditor.CursorPosition;
                    var (cursorRow, cursorCol) = GetCursorRowColFromPosition(cursorPos, lines);

                    // Only draw if cursor is in visible area
                    if (cursorRow >= startLine && cursorRow < endLine)
                    {
                        var visibleRow = cursorRow - startLine;
                        var lineText = lines.Count > cursorRow ? lines[cursorRow] : "";
                        var charsToMeasure = Math.Min(cursorCol, lineText.Length);
                        var textWidth = charsToMeasure > 0
                            ? font.MeasureString(lineText.Substring(0, charsToMeasure)).Width
                            : 0;

                        var caretX = editorX + (int)textWidth;
                        var caretY = editorY + visibleRow * lineHeight;
                        var caretHeight = lineHeight - 2;

                        _spriteBatch.Draw(_caretTexture, new Rectangle(caretX, caretY, 1, caretHeight), Color.White);
                    }
                }
            }
        }

        /// <summary>
        /// Draws the editor text content in post-scale phase for crisp rendering
        /// </summary>
        private void DrawEditorContentPostScale(Vector2 scaledPos, float scale)
        {
            var font = FontScaleHelper.GetScaledFont(_contentProvider, scale);

            var editorX = (int)(scaledPos.X + 12 * scale);
            var editorY = (int)(scaledPos.Y + ListAreaTop * scale);
            var editorWidth = (int)((DialogWidth - 48) * scale);
            var editorHeight = (int)(ListAreaHeight * scale);
            var lineHeight = (int)(14 * scale);

            // Word wrap the content to fit the editor width
            var lines = WrapText(_editorContent, font, editorWidth);

            _spriteBatch.Begin();

            // Draw each visible line
            var startLine = Math.Max(0, _editorScrollOffset);
            var linesPerPage = editorHeight / lineHeight;
            var endLine = Math.Min(lines.Count, startLine + linesPerPage);

            for (var i = startLine; i < endLine; i++)
            {
                var lineY = editorY + (i - startLine) * lineHeight;
                _spriteBatch.DrawString(font, lines[i], new Vector2(editorX, lineY), StyleProvider.TextPrimary);
            }

            // Draw blinking caret if textbox is selected
            if (_contentEditor.Selected && _caretTexture != null)
            {
                // Blink every 500ms (same as XNATextBox)
                var showCaret = !(_caretBlinkTimer % 1000 < 500);
                if (showCaret)
                {
                    // Calculate cursor row/col based on OUR wrapping (not textbox's)
                    var cursorPos = _contentEditor.CursorPosition;
                    var (cursorRow, cursorCol) = GetCursorRowColFromPosition(cursorPos, lines);

                    // Only draw if cursor is in visible area
                    if (cursorRow >= startLine && cursorRow < endLine)
                    {
                        var visibleRow = cursorRow - startLine;
                        var lineText = lines.Count > cursorRow ? lines[cursorRow] : "";
                        var charsToMeasure = Math.Min(cursorCol, lineText.Length);
                        var textWidth = charsToMeasure > 0
                            ? font.MeasureString(lineText.Substring(0, charsToMeasure)).Width
                            : 0;

                        var caretX = editorX + (int)textWidth;
                        var caretY = editorY + visibleRow * lineHeight;

                        _spriteBatch.Draw(_caretTexture, new Vector2(caretX, caretY), Color.White);
                    }
                }
            }

            _spriteBatch.End();
        }

        /// <summary>
        /// Converts a raw cursor position (character index) to row/column based on wrapped lines
        /// </summary>
        private (int row, int col) GetCursorRowColFromPosition(int cursorPos, List<string> lines)
        {
            var charsProcessed = 0;
            for (var row = 0; row < lines.Count; row++)
            {
                var lineLen = lines[row].Length;
                // Account for newline that we processed during wrapping
                var lineEnd = charsProcessed + lineLen;

                if (cursorPos <= lineEnd)
                {
                    return (row, cursorPos - charsProcessed);
                }

                charsProcessed = lineEnd + 1; // +1 for newline
            }

            // Cursor at end of text
            return (Math.Max(0, lines.Count - 1), lines.Count > 0 ? lines[^1].Length : 0);
        }

        /// <summary>
        /// Word-wraps text to fit within the specified width
        /// </summary>
        private List<string> WrapText(string text, BitmapFont font, float maxWidth)
        {
            var result = new List<string>();
            var paragraphs = text.Split('\n');

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrEmpty(paragraph))
                {
                    result.Add(string.Empty);
                    continue;
                }

                var currentLine = string.Empty;

                // For hard wrapping, check character by character if needed
                foreach (var ch in paragraph)
                {
                    var testLine = currentLine + ch;
                    var size = font.MeasureString(testLine);

                    if (size.Width > maxWidth && currentLine.Length > 0)
                    {
                        result.Add(currentLine);
                        currentLine = ch.ToString();
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (currentLine.Length > 0)
                    result.Add(currentLine);
            }

            return result;
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
            _editorContent = content;
            SetState(IniEditorState.EditFile);
        }

        public void NotifyIniFileSaveResult(bool success, string message)
        {
            // Temporarily suppress post-scale rendering so message box appears on top
            SuppressPostScaleRendering = true;

            if (success)
            {
                var dlg = _messageBoxFactory.CreateMessageBox("Save Successful", $"File saved successfully.\n\nNote: Run $rehash to apply changes.");
                dlg.DialogClosing += (_, _) => SuppressPostScaleRendering = false;
                dlg.ShowDialog();
                _contentModified = false;
                SetState(IniEditorState.FileList);
            }
            else
            {
                var dlg = _messageBoxFactory.CreateMessageBox("Save Failed", $"Failed to save file: {message}");
                dlg.DialogClosing += (_, _) => SuppressPostScaleRendering = false;
                dlg.ShowDialog();
            }
        }
    }
}
