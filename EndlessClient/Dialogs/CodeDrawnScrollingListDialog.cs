using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Services;
using EndlessClient.GameExecution;
using EndlessClient.Rendering;
using EndlessClient.Services;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Graphics;
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
    /// Implements IPostScaleDrawable for crisp text rendering when the client window is scaled.
    /// </summary>
    public class CodeDrawnScrollingListDialog : XNADialog, IPostScaleDrawable
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly Func<bool> _isInGame;
        private readonly List<CodeDrawnListItem> _listItems;
        private readonly CodeDrawnScrollBar _scrollBar;
        private readonly BitmapFont _font;
        private readonly BitmapFont _scaledFont;

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

        // Expose for child classes
        protected IUIStyleProvider StyleProvider => _styleProvider;
        protected IClientWindowSizeProvider ClientWindowSizeProvider => _clientWindowSizeProvider;
        protected BitmapFont Font => _font;
        protected BitmapFont ScaledFont => _scaledFont;

        /// <summary>
        /// Constructor with full post-scale rendering support.
        /// </summary>
        public CodeDrawnScrollingListDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            IClientWindowSizeProvider clientWindowSizeProvider,
            IGraphicsDeviceProvider graphicsDeviceProvider,
            BitmapFont font,
            BitmapFont scaledFont)
            : this(styleProvider, gameStateProvider, font)
        {
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _scaledFont = scaledFont;

            // In scaled mode, we draw the title manually in DrawPostScale
            // Unparent the XNALabel so it doesn't draw fuzzy text in render target phase
            if (_clientWindowSizeProvider?.IsScaledMode ?? false)
            {
                _titleText?.SetControlUnparented();
            }
        }

        /// <summary>
        /// Backwards-compatible constructor without post-scale rendering support.
        /// Post-scale rendering will be disabled for dialogs using this constructor.
        /// </summary>
        public CodeDrawnScrollingListDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            BitmapFont font)
        {
            _styleProvider = styleProvider;
            _clientWindowSizeProvider = null;
            _graphicsDeviceProvider = null;
            _isInGame = () => gameStateProvider.CurrentState == GameStates.PlayingTheGame;
            _font = font;
            _scaledFont = font; // Use same font when no scaled font provided
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

        public void AddItem(string primaryText, string subText = "", object data = null, Action<CodeDrawnListItem> onClick = null, Action<CodeDrawnListItem> onRightClick = null, bool isLink = false, Texture2D icon = null)
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
                var item = new CodeDrawnListItem(_styleProvider, _font, this, _clientWindowSizeProvider)
                {
                    PrimaryText = lineText,
                    SubText = i == 0 ? subText : "",
                    Data = data,
                    Index = _listItems.Count,
                    IsLink = isLink && (i == 0), // Only first line is clickable for links
                    IconGraphic = i == 0 ? icon : null // Only first line shows icon
                };

                // Only attach click handlers to first line for links, or to all items if not a link
                if (onClick != null && (i == 0 || !isLink))
                    item.LeftClick += (_, _) => onClick(item);

                if (onRightClick != null && (i == 0 || !isLink))
                    item.RightClick += (_, _) => onRightClick(item);

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
            if (_graphicsDeviceProvider != null)
                DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);
            else
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

        // IPostScaleDrawable implementation
        public bool SkipRenderTargetDraw => _clientWindowSizeProvider?.IsScaledMode ?? false;

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (SkipRenderTargetDraw)
            {
                // In scaled mode, draw only fills during the render target phase
                DrawFills(DrawAreaWithParentOffset);
                base.OnDrawControl(gameTime);
                return;
            }

            // Non-scaled mode: draw everything together
            DrawComplete(DrawAreaWithParentOffset);
            base.OnDrawControl(gameTime);
        }

        public virtual void DrawPostScale(SpriteBatch spriteBatch, float scaleFactor, Point renderOffset)
        {
            if (!Visible) return;

            var gamePos = DrawAreaWithParentOffset;
            var scaledPos = new Vector2(
                gamePos.X * scaleFactor + renderOffset.X,
                gamePos.Y * scaleFactor + renderOffset.Y);

            DrawBordersAndText(scaledPos, scaleFactor);
        }

        /// <summary>
        /// Draws only fills (for render target in scaled mode)
        /// </summary>
        protected virtual void DrawFills(Rectangle drawPos)
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);
            var bounds = new Rectangle(0, 0, DrawArea.Width, DrawArea.Height);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main panel background
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, bounds, _styleProvider.PanelBackground, cornerRadius);

            // Title bar fill
            DrawingPrimitives.DrawFilledRect(_spriteBatch,
                new Rectangle(borderThickness, borderThickness, DrawArea.Width - borderThickness * 2, titleBarHeight - borderThickness),
                _styleProvider.TitleBarBackground);

            // List area background (slightly darker)
            var listBounds = new Rectangle(8, ListAreaTop, DialogWidth - 40, ListAreaHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, listBounds, new Color(0, 0, 0, 60));

            _spriteBatch.End();
        }

        /// <summary>
        /// Draws borders and text post-scale for crisp rendering
        /// </summary>
        protected virtual void DrawBordersAndText(Vector2 scaledPos, float scale)
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;

            // Select font based on scale - 3 tier system
            BitmapFont font;
            if (scale >= 1.75f) font = _scaledFont;
            else if (scale >= 1.25f) font = _scaledFont; // Medium scale uses scaled font
            else font = _font;

            var panelWidth = (int)(DialogWidth * scale);
            var panelHeight = (int)(DialogHeight * scale);

            _spriteBatch.Begin();

            // Panel border
            var bgRect = new Rectangle((int)scaledPos.X, (int)scaledPos.Y, panelWidth, panelHeight);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, cornerRadius, Math.Max(1, (int)(borderThickness * scale)));

            // Title text
            var title = Title;
            if (!string.IsNullOrEmpty(title))
            {
                _spriteBatch.DrawString(font, title, new Vector2(scaledPos.X + 16 * scale, scaledPos.Y + 12 * scale), _styleProvider.TitleBarText);
            }

            // List area border
            var listRect = new Rectangle(
                (int)(scaledPos.X + 8 * scale),
                (int)(scaledPos.Y + ListAreaTop * scale),
                (int)((DialogWidth - 40) * scale),
                (int)(ListAreaHeight * scale));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, listRect, _styleProvider.PanelBorder, 1);

            // Draw visible list items
            for (int i = 0; i < _listItems.Count; i++)
            {
                var item = _listItems[i];
                if (i >= ScrollOffset && i < ScrollOffset + ItemsToShow)
                {
                    var visualIndex = i - ScrollOffset;
                    var itemY = (int)(scaledPos.Y + (ListAreaTop + visualIndex * ItemHeight) * scale);

                    // Item hover background
                    if (item.IsHovered && item.IsLink)
                    {
                        var hoverRect = new Rectangle(
                            (int)(scaledPos.X + 8 * scale),
                            itemY,
                            (int)((DialogWidth - 48) * scale),
                            (int)(ItemHeight * scale));
                        DrawingPrimitives.DrawFilledRect(_spriteBatch, hoverRect, new Color(255, 255, 255, 30));
                    }

                    // Item text
                    var textColor = item.IsLink
                        ? (item.IsHovered ? new Color(150, 230, 255) : _styleProvider.TextHighlight)
                        : _styleProvider.TextPrimary;

                    var textOffsetX = item.IconGraphic != null ? 40 : 4;
                    _spriteBatch.DrawString(font, item.PrimaryText,
                        new Vector2(scaledPos.X + (8 + textOffsetX) * scale, itemY + 2 * scale), textColor);

                    // Sub text (right aligned)
                    if (!string.IsNullOrEmpty(item.SubText))
                    {
                        var subSize = font.MeasureString(item.SubText);
                        var subX = scaledPos.X + (DialogWidth - 40 - 4) * scale - subSize.Width;
                        _spriteBatch.DrawString(font, item.SubText, new Vector2(subX, itemY + 2 * scale), _styleProvider.TextSecondary);
                    }

                    // Icon
                    if (item.IconGraphic != null)
                    {
                        var iconSize = (int)(Math.Min(32, ItemHeight - 2) * scale);
                        var iconY = itemY + (int)((ItemHeight * scale - iconSize) / 2);
                        _spriteBatch.Draw(item.IconGraphic,
                            new Rectangle((int)(scaledPos.X + 12 * scale), iconY, iconSize, iconSize), Color.White);
                    }
                }
            }

            // Draw button text (buttons themselves draw their fills)
            DrawButtonTextPostScale(scaledPos, scale, font);

            _spriteBatch.End();
        }

        /// <summary>
        /// Draws button borders and text in post-scale phase
        /// </summary>
        protected void DrawButtonTextPostScale(Vector2 scaledPos, float scale, BitmapFont font)
        {
            var buttonWidth = (int)(72 * scale);
            var buttonHeight = (int)(28 * scale);
            var buttonY = (int)(scaledPos.Y + (DialogHeight - 28 - 12) * scale);
            var buttonSpacing = (int)(16 * scale);

            if (_okButton != null && _cancelButton != null)
            {
                // Two buttons
                DrawButtonPostScale("OK", (int)(scaledPos.X + (DialogWidth / 2 - 72 - 8) * scale), buttonY, buttonWidth, buttonHeight, scale, font, _okButton.MouseOver);
                DrawButtonPostScale("Cancel", (int)(scaledPos.X + (DialogWidth / 2 + 8) * scale), buttonY, buttonWidth, buttonHeight, scale, font, _cancelButton.MouseOver);
            }
            else if (_okButton != null)
            {
                // Single OK button
                DrawButtonPostScale("OK", (int)(scaledPos.X + (DialogWidth - 72) / 2 * scale), buttonY, buttonWidth, buttonHeight, scale, font, _okButton.MouseOver);
            }
            else if (_cancelButton != null)
            {
                // Single Cancel button
                DrawButtonPostScale("Cancel", (int)(scaledPos.X + (DialogWidth - 72) / 2 * scale), buttonY, buttonWidth, buttonHeight, scale, font, _cancelButton.MouseOver);
            }

            if (_backButton != null)
            {
                DrawButtonPostScale("Back", (int)(scaledPos.X + (DialogWidth / 2 - 72 - 8) * scale), buttonY, buttonWidth, buttonHeight, scale, font, _backButton.MouseOver);
            }
        }

        private void DrawButtonPostScale(string text, int x, int y, int width, int height, float scale, BitmapFont font, bool isHovered)
        {
            var buttonRect = new Rectangle(x, y, width, height);
            var buttonColor = isHovered ? _styleProvider.ButtonHover : _styleProvider.ButtonNormal;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, buttonRect, buttonColor);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, buttonRect, Color.Black, 1);

            var textSize = font.MeasureString(text);
            var textPos = new Vector2(
                x + (width - textSize.Width) / 2,
                y + (height - textSize.Height) / 2);
            _spriteBatch.DrawString(font, text, textPos, Color.White);
        }

        /// <summary>
        /// Complete drawing for non-scaled mode
        /// </summary>
        private void DrawComplete(Rectangle drawPos)
        {
            var cornerRadius = _styleProvider.CornerRadius;
            var borderThickness = _styleProvider.BorderThickness;
            var titleBarHeight = _styleProvider.TitleBarHeight;

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
        }

        /// <summary>
        /// Transform mouse position from screen space to game space for scaled mode
        /// </summary>
        protected Point TransformMousePosition(Point position)
        {
            if (_clientWindowSizeProvider == null || !_clientWindowSizeProvider.IsScaledMode)
                return position;

            var offset = _clientWindowSizeProvider.RenderOffset;
            var scale = _clientWindowSizeProvider.ScaleFactor;

            int gameX = (int)((position.X - offset.X) / scale);
            int gameY = (int)((position.Y - offset.Y) / scale);

            return new Point(
                Math.Clamp(gameX, 0, _clientWindowSizeProvider.GameWidth - 1),
                Math.Clamp(gameY, 0, _clientWindowSizeProvider.GameHeight - 1));
        }

        // Absorb all clicks on the dialog background to prevent click-through to underlying controls
        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs) => true;

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs) => true;

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
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
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

        public bool IsHovered => _isHovered;

        public event EventHandler<MouseEventArgs> LeftClick;
        public event EventHandler<MouseEventArgs> RightClick;

        public CodeDrawnListItem(IUIStyleProvider styleProvider, BitmapFont font, CodeDrawnScrollingListDialog parent, IClientWindowSizeProvider clientWindowSizeProvider)
        {
            _styleProvider = styleProvider;
            _font = font;
            _parentDialog = parent;
            _clientWindowSizeProvider = clientWindowSizeProvider;

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
            // In scaled mode, drawing is handled by parent's DrawPostScale
            if (_clientWindowSizeProvider?.IsScaledMode ?? false)
            {
                base.OnDrawControl(gameTime);
                return;
            }

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
