using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A procedurally-drawn scrolling list dialog that replaces texture-based dialogs when UIMode=Code.
    /// </summary>
    public class CodeDrawnScrollingListDialog : XNADialog
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly Func<bool> _isInGame;
        private readonly List<CodeDrawnListItem> _listItems;
        private readonly CodeDrawnScrollBar _scrollBar;
        private readonly BitmapFont _font;

        private IXNALabel _titleText;
        private CodeDrawnButton _okButton;
        private CodeDrawnButton _cancelButton;
        private CodeDrawnButton _backButton;

        public int DialogWidth { get; set; } = 290;
        public int DialogHeight { get; set; } = 320;
        public int ListAreaTop { get; set; } = 45;
        public int ListAreaHeight { get; set; } = 230;
        public int ItemHeight { get; set; } = 20;

        public int ItemsToShow => ListAreaHeight / ItemHeight;
        public int ScrollOffset => _scrollBar?.ScrollOffset ?? 0;

        public string Title
        {
            get => _titleText?.Text ?? string.Empty;
            set { if (_titleText != null) _titleText.Text = value; }
        }

        public IReadOnlyList<string> NamesList => _listItems.Select(item => item.PrimaryText).ToList();

        public event EventHandler BackAction;
        public event EventHandler OkAction;
        public event EventHandler CancelAction;

        public CodeDrawnScrollingListDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            BitmapFont font)
        {
            _styleProvider = styleProvider;
            _isInGame = () => gameStateProvider.CurrentState == GameStates.PlayingTheGame;
            _font = font;
            _listItems = new List<CodeDrawnListItem>();

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            // Title label
            _titleText = new XNALabel(Constants.FontSize08pt5)
            {
                AutoSize = false,
                DrawArea = new Rectangle(16, 12, DialogWidth - 32, 20),
                ForeColor = _styleProvider.TitleBarText,
                TextAlign = LabelAlignment.MiddleLeft,
                Text = string.Empty
            };
            _titleText.SetParentControl(this);

            // Scrollbar
            _scrollBar = new CodeDrawnScrollBar(_styleProvider, new Vector2(DialogWidth - 24, ListAreaTop), new Vector2(16, ListAreaHeight))
            {
                LinesToRender = ItemsToShow
            };
            _scrollBar.SetParentControl(this);
        }

        /// <summary>
        /// Updates the scrollbar position and size after dialog dimensions change.
        /// Call this after modifying DialogWidth, DialogHeight, ListAreaTop, ListAreaHeight, or ItemHeight.
        /// </summary>
        protected void UpdateScrollBarLayout()
        {
            _scrollBar.DrawArea = new Rectangle(DialogWidth - 24, ListAreaTop, 16, ListAreaHeight);
            _scrollBar.LinesToRender = ItemsToShow;
            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);
        }

        /// <summary>
        /// Closes the dialog with Cancel result. This is a public wrapper around the protected Close method.
        /// </summary>
        public void Close()
        {
            Close(XNADialogResult.Cancel);
        }

        public void SetupButtons(bool showOk = true, bool showCancel = true, bool showBack = false)
        {
            var buttonWidth = 72;
            var buttonHeight = 28;
            var buttonY = DialogHeight - buttonHeight - 12;
            var buttonSpacing = 16;

            if (showBack)
            {
                _backButton = CreateButton("Back", new Vector2(DialogWidth / 2 - buttonWidth - buttonSpacing / 2, buttonY), buttonWidth, buttonHeight);
                _backButton.OnClick += (_, _) => BackAction?.Invoke(this, EventArgs.Empty);
            }

            if (showOk && showCancel)
            {
                _okButton = CreateButton("OK", new Vector2(DialogWidth / 2 - buttonWidth - buttonSpacing / 2, buttonY), buttonWidth, buttonHeight);
                _okButton.OnClick += (_, _) => { OkAction?.Invoke(this, EventArgs.Empty); Close(XNADialogResult.OK); };

                _cancelButton = CreateButton("Cancel", new Vector2(DialogWidth / 2 + buttonSpacing / 2, buttonY), buttonWidth, buttonHeight);
                _cancelButton.OnClick += (_, _) => { CancelAction?.Invoke(this, EventArgs.Empty); Close(XNADialogResult.Cancel); };
            }
            else if (showOk)
            {
                _okButton = CreateButton("OK", new Vector2((DialogWidth - buttonWidth) / 2, buttonY), buttonWidth, buttonHeight);
                _okButton.OnClick += (_, _) => { OkAction?.Invoke(this, EventArgs.Empty); Close(XNADialogResult.OK); };
            }
            else if (showCancel)
            {
                _cancelButton = CreateButton("Cancel", new Vector2((DialogWidth - buttonWidth) / 2, buttonY), buttonWidth, buttonHeight);
                _cancelButton.OnClick += (_, _) => { CancelAction?.Invoke(this, EventArgs.Empty); Close(XNADialogResult.Cancel); };
            }

            CenterInGameView();
        }

        private CodeDrawnButton CreateButton(string text, Vector2 position, int width, int height)
        {
            var button = new CodeDrawnButton(_styleProvider, _font)
            {
                Text = text,
                DrawArea = new Rectangle((int)position.X, (int)position.Y, width, height)
            };
            button.SetParentControl(this);
            return button;
        }

        public void AddItem(string primaryText, string subText = "", object data = null, Action<CodeDrawnListItem> onClick = null, bool isLink = false, Texture2D icon = null)
        {
            // Calculate max width for text (with some padding)
            var maxTextWidth = DialogWidth - 56;

            // Split text into lines if it's too long
            var words = primaryText.Split(' ');
            var currentLine = "";
            var lines = new System.Collections.Generic.List<string>();

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var size = _font.MeasureString(testLine);

                if (size.Width > maxTextWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            // Add each line as a separate item
            for (int i = 0; i < lines.Count; i++)
            {
                var lineText = lines[i];
                var item = new CodeDrawnListItem(_styleProvider, _font, this)
                {
                    PrimaryText = lineText,
                    SubText = i == 0 ? subText : "",
                    Data = data,
                    Index = _listItems.Count,
                    IsLink = isLink && (i == 0), // Only first line is clickable for links
                    IconGraphic = i == 0 ? icon : null // Only first line shows icon
                };

                // Only attach click handler to first line for links, or to all items if not a link
                if (onClick != null && (i == 0 || !isLink))
                    item.LeftClick += (_, _) => onClick(item);

                _listItems.Add(item);
            }

            _scrollBar.UpdateDimensions(_listItems.Count);
        }

        public void ClearItems()
        {
            foreach (var item in _listItems.ToList())
            {
                item.SetControlUnparented();
                item.Dispose();
            }
            _listItems.Clear();
            _scrollBar.UpdateDimensions(0);
            _scrollBar.ScrollToTop();
        }

        public override void CenterInGameView()
        {
            // Use logical game dimensions (640x480 equivalent) for proper centering in scaled mode
            int centerWidth, centerHeight;
            if (XNADialog.GameViewportProvider != null)
            {
                centerWidth = XNADialog.GameViewportProvider.GameWidth;
                centerHeight = XNADialog.GameViewportProvider.GameHeight;
            }
            else
            {
                centerWidth = Game.GraphicsDevice.Viewport.Width;
                centerHeight = Game.GraphicsDevice.Viewport.Height;
            }

            var centerX = (centerWidth - DialogWidth) / 2;
            var centerY = (centerHeight - DialogHeight) / 2;
            DrawPosition = new Vector2(centerX, centerY);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);

            _titleText?.Initialize();
            _scrollBar?.Initialize();
            _okButton?.Initialize();
            _cancelButton?.Initialize();
            _backButton?.Initialize();

            foreach (var item in _listItems)
                item.Initialize();

            base.Initialize();

            // Ensure proper centering after initialization (Game reference now available)
            CenterInGameView();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Update item visibility based on scroll
            for (int i = 0; i < _listItems.Count; i++)
            {
                var item = _listItems[i];
                if (i < ScrollOffset || i >= ScrollOffset + ItemsToShow)
                {
                    item.Visible = false;
                }
                else
                {
                    item.Visible = true;
                    item.VisualIndex = i - ScrollOffset;
                }
            }

            base.OnUpdateControl(gameTime);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main panel background
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bounds, _styleProvider.PanelBorder, cornerRadius, borderThickness);

            // Title bar
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle(borderThickness, borderThickness, DrawArea.Width - borderThickness * 2, titleBarHeight - borderThickness),
                _styleProvider.TitleBarBackground);

            // List area background (slightly darker)
            var listBounds = new Rectangle(8, ListAreaTop, DialogWidth - 40, ListAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, listBounds, new Color(0, 0, 0, 60));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, listBounds, _styleProvider.PanelBorder, 1);

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleMouseWheelMoved(IXNAControl control, MouseEventArgs eventArgs)
        {
            // Forward scroll wheel events to the scrollbar
            if (eventArgs.ScrollWheelDelta > 0)
                _scrollBar.ScrollUp(2);
            else if (eventArgs.ScrollWheelDelta < 0)
                _scrollBar.ScrollDown(2);

            return true;
        }
    }

    /// <summary>
    /// A single item in a code-drawn scrolling list dialog.
    /// </summary>
    public class CodeDrawnListItem : XNAControl
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly BitmapFont _font;
        private readonly CodeDrawnScrollingListDialog _parentDialog;
        private bool _isHovered;

        public int Index { get; set; }
        public int VisualIndex { get; set; }
        public string PrimaryText { get; set; } = string.Empty;
        public string SubText { get; set; } = string.Empty;
        public object Data { get; set; }
        public bool IsLink { get; set; }
        public Texture2D IconGraphic { get; set; }

        public event EventHandler<MouseEventArgs> LeftClick;
        public event EventHandler<MouseEventArgs> RightClick;

        public CodeDrawnListItem(IUIStyleProvider styleProvider, BitmapFont font, CodeDrawnScrollingListDialog parent)
        {
            _styleProvider = styleProvider;
            _font = font;
            _parentDialog = parent;

            SetSize(parent.DialogWidth - 48, 18);
            SetParentControl(parent);

            OnMouseEnter += (_, _) => _isHovered = true;
            OnMouseLeave += (_, _) => _isHovered = false;
        }

        public override Rectangle DrawArea
        {
            get => new Rectangle(8, _parentDialog.ListAreaTop + (VisualIndex * _parentDialog.ItemHeight), base.DrawArea.Width, base.DrawArea.Height);
            set => base.DrawArea = value;
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            _spriteBatch.Begin(transformMatrix: transform);

            // Hover background (only for links)
            if (_isHovered && IsLink)
            {
                DrawingPrimitives.DrawFilledRect(_spriteBatch,
                    new Rectangle(0, 0, DrawArea.Width, DrawArea.Height),
                    new Color(255, 255, 255, 30));
            }

            // Calculate text offset (shifted right if icon present)
            var textOffsetX = IconGraphic != null ? 40 : 4;

            // Icon (if present)
            if (IconGraphic != null)
            {
                var iconSize = Math.Min(32, DrawArea.Height - 2);
                var iconY = (DrawArea.Height - iconSize) / 2;
                _spriteBatch.Draw(IconGraphic, new Rectangle(4, iconY, iconSize, iconSize), Color.White);
            }

            // Primary text
            if (!string.IsNullOrEmpty(PrimaryText))
            {
                Color textColor;
                if (IsLink)
                {
                    // Links always use highlight color, brighter when hovered
                    textColor = _isHovered ? new Color(150, 230, 255) : _styleProvider.TextHighlight;
                }
                else
                {
                    // Normal text always uses primary color (no hover effect)
                    textColor = _styleProvider.TextPrimary;
                }
                _spriteBatch.DrawString(_font, PrimaryText, new Vector2(textOffsetX, 2), textColor);
            }

            // Sub text (right aligned)
            if (!string.IsNullOrEmpty(SubText))
            {
                var subSize = _font.MeasureString(SubText);
                var subPos = new Vector2(DrawArea.Width - subSize.Width - 4, 2);
                _spriteBatch.DrawString(_font, SubText, subPos, _styleProvider.TextSecondary);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            LeftClick?.Invoke(this, eventArgs);
            return true;
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MonoGame.Extended.Input.MouseButton.Right)
                RightClick?.Invoke(this, eventArgs);

            return true;
        }
    }
}
