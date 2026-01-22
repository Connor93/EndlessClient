using System;
using EndlessClient.Content;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Graphics;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using Optional;
using XNAControls;

namespace EndlessClient.HUD.StatusBars
{
    /// <summary>
    /// Code-drawn status bar base class that renders using the current UI style
    /// </summary>
    public abstract class CodeDrawnStatusBarBase : XNAControl
    {
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        protected readonly XNALabel _label;
        private readonly BitmapFont _font;
        private Texture2D _pixel;

        protected CharacterStats Stats => _characterProvider.MainCharacter.Stats;

        protected abstract int StatusBarIndex { get; }
        protected abstract Color BarFillColor { get; }
        protected abstract string BarLabel { get; }

        private Option<DateTime> _labelShowTime;

        public event Action StatusBarClicked;
        public event Action StatusBarClosed;

        private const int BarWidth = 110;
        private const int BarHeight = 14;
        private const int FillPadding = 2;
        private const int DropDownHeight = 21;

        protected CodeDrawnStatusBarBase(IClientWindowSizeProvider clientWindowSizeProvider,
                                         ICharacterProvider characterProvider,
                                         IUIStyleProvider styleProvider,
                                         IGraphicsDeviceProvider graphicsDeviceProvider,
                                         IContentProvider contentProvider)
        {
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _characterProvider = characterProvider;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];

            _label = new XNALabel(Constants.FontSize08)
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                DrawPosition = new Vector2(6, 15),
                ForeColor = _styleProvider.TextPrimary,
                Visible = false
            };
            _label.SetParentControl(this);

            DrawArea = new Rectangle(0, 0, BarWidth, BarHeight);

            if (_clientWindowSizeProvider.Resizable)
                _clientWindowSizeProvider.GameWindowSizeChanged += (o, e) => ChangeStatusBarPosition();
        }

        protected abstract void UpdateLabelText();
        protected abstract float GetFillPercentage();

        public override void Initialize()
        {
            _pixel = new Texture2D(_graphicsDeviceProvider.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Ensure DrawingPrimitives is initialized
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            _label.Initialize();
            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            _labelShowTime.MatchSome(x =>
            {
                UpdateLabelText();

                if ((DateTime.Now - x).TotalSeconds >= 4)
                {
                    _label.Visible = false;
                    _labelShowTime = Option.None<DateTime>();

                    StatusBarClosed?.Invoke();
                }
            });

            base.OnUpdateControl(gameTime);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;
            var barRect = new Rectangle((int)pos.X, (int)pos.Y, BarWidth, BarHeight);

            // Draw background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, barRect, _styleProvider.StatusBarBackground);

            // Draw border
            DrawingPrimitives.DrawRectBorder(_spriteBatch, barRect, _styleProvider.StatusBarBorder, 1);

            // Draw fill bar based on percentage
            var fillPercentage = Math.Clamp(GetFillPercentage(), 0f, 1f);
            var fillWidth = (int)((BarWidth - FillPadding * 2) * fillPercentage);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle((int)pos.X + FillPadding, (int)pos.Y + FillPadding, fillWidth, BarHeight - FillPadding * 2);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, fillRect, BarFillColor);

                // Add subtle gradient highlight on top
                var highlightColor = new Color(255, 255, 255, 40);
                var highlightRect = new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, fillRect.Height / 2);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, highlightRect, highlightColor);
            }

            // Draw bar label (HP, TP, SP, TNL) on left edge with dark background
            var labelText = BarLabel;
            var textSize = _font.MeasureString(labelText);
            var labelBgRect = new Rectangle((int)pos.X - (int)textSize.Width - 4, (int)pos.Y, (int)textSize.Width + 4, BarHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, labelBgRect, new Color(0, 0, 0, 180));
            var labelPos = new Vector2(pos.X - textSize.Width - 2, pos.Y + (BarHeight - textSize.Height) / 2);
            _spriteBatch.DrawString(_font, labelText, labelPos, Color.White);

            _spriteBatch.End();

            // Draw dropdown if visible
            if (_labelShowTime.HasValue)
            {
                _spriteBatch.Begin();

                var dropdownRect = new Rectangle((int)pos.X, (int)pos.Y + BarHeight - 3, BarWidth, DropDownHeight);
                DrawingPrimitives.DrawFilledRect(_spriteBatch, dropdownRect, _styleProvider.StatusBarBackground);
                DrawingPrimitives.DrawRectBorder(_spriteBatch, dropdownRect, _styleProvider.StatusBarBorder, 1);

                _spriteBatch.End();
            }

            base.OnDrawControl(gameTime);
        }

        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            _label.Visible = !_label.Visible;
            _labelShowTime = _label.SomeWhen(x => x.Visible).Map(_ => DateTime.Now);

            StatusBarClicked?.Invoke();

            return true;
        }

        protected void ChangeStatusBarPosition()
        {
            // Add extra spacing between bars to account for label width (label is drawn to the left of bar)
            var barSpacing = DrawArea.Width + 40; // Extra 40px for label
            var xCoord = (_clientWindowSizeProvider.Width / 2) + StatusBarIndex * barSpacing;
            DrawPosition = new Vector2(xCoord, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pixel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
