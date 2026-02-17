using System;
using EndlessClient.Content;
using EndlessClient.GameExecution;
using EOLib.Shared;
using EOLib.Config;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using Optional;

namespace EndlessClient.Rendering
{
    public class HealthBarRenderer : DrawableGameComponent, IHealthBarRenderer
    {
        // Bar dimensions (unzoomed pixels)
        private const int BarWidth = 64;
        private const int BarHeight = 12;
        private const int PlatePadding = 3;
        private const int PlateWidth = BarWidth + PlatePadding * 2;
        private const int PlateHeight = BarHeight + PlatePadding * 2;

        // Timing
        private const float DamageTextDuration = 12f;       // frame-offset units for floating text
        private const double BarVisibilitySeconds = 5.0;    // health bar stays visible for 5s

        // Colors
        private static readonly Color BarFillColor = new Color(200, 35, 35);
        private static readonly Color PlateColor = new Color(0, 0, 0, 160);
        private static readonly Color DamageTextColor = new Color(255, 220, 80);   // warm yellow-orange
        private static readonly Color HealTextColor = new Color(100, 220, 100);     // green
        private static readonly Color MissTextColor = new Color(200, 200, 200);     // soft grey
        private static readonly Color HpTextColor = Color.White;

        private readonly IMapActor _parentReference;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IContentProvider _contentProvider;

        private readonly SpriteBatch _spriteBatch;
        private Texture2D _pixelTexture;

        private Action _doneCallback;
        private bool _isMiss;
        private bool _isHeal;
        private int _percentHealth;
        private string _damageText;
        private float _frameOffset;
        private bool _damageTextVisible;

        // Time-based bar visibility
        private DateTime _lastDamageTime;
        private bool _barVisible;

        private Vector2 _healthBarPosition;
        private Vector2 _damageCounterPosition;

        public HealthBarRenderer(IEndlessGameProvider endlessGameProvider,
                                 IClientWindowSizeProvider clientWindowSizeProvider,
                                 IConfigurationProvider configurationProvider,
                                 IContentProvider contentProvider,
                                 IMapActor parentReference)
            : base((Game)endlessGameProvider.Game)
        {
            _parentReference = parentReference;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            _configurationProvider = configurationProvider;
            _contentProvider = contentProvider;

            _spriteBatch = new SpriteBatch(Game.GraphicsDevice);

            _damageText = string.Empty;
            UpdateOrder = DrawOrder = 99;
            Enabled = Visible = false;
        }

        public override void Initialize()
        {
            _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            base.Initialize();
        }

        public void SetDamage(Option<int> value, int percentHealth, Action doneCallback = null)
        {
            Enabled = Visible = true;
            _doneCallback = doneCallback;
            _frameOffset = 0;
            _percentHealth = percentHealth;
            _isHeal = false;
            _damageTextVisible = true;
            _barVisible = true;
            _lastDamageTime = DateTime.Now;

            value.Match(
                some: v =>
                {
                    _isMiss = false;
                    _damageText = v.ToString();
                },
                none: () =>
                {
                    _isMiss = true;
                    _damageText = "MISS";
                });
        }

        public void SetHealth(int value, int percentHealth, Action doneCallback = null)
        {
            Enabled = Visible = true;
            _doneCallback = doneCallback;
            _frameOffset = 0;
            _percentHealth = percentHealth;
            _isHeal = true;
            _isMiss = false;
            _damageText = value.ToString();
            _damageTextVisible = true;
            _barVisible = true;
            _lastDamageTime = DateTime.Now;
        }

        public override void Update(GameTime gameTime)
        {
            // Damage text floats and fades independently
            _frameOffset += .1f;
            if (_frameOffset > DamageTextDuration)
                _damageTextVisible = false;

            // Health bar stays visible for BarVisibilitySeconds, but hides immediately if actor is dead
            var elapsed = (DateTime.Now - _lastDamageTime).TotalSeconds;
            if (!_parentReference.IsAlive || elapsed > BarVisibilitySeconds)
            {
                _barVisible = false;
            }

            // When both text and bar are done, hide the component entirely
            if (!_damageTextVisible && !_barVisible)
            {
                Enabled = Visible = false;
                _doneCallback?.Invoke();
            }

            // Calculate positions by zooming the CENTER POINT first, then offsetting
            var zoom = _configurationProvider.MapZoom;
            var centerX = _clientWindowSizeProvider.GameWidth / 2f;
            var centerY = _clientWindowSizeProvider.GameHeight / 2f;

            float zoomedHCenter, zoomedNameY;
            if (zoom != 1.0f)
            {
                zoomedHCenter = (_parentReference.HorizontalCenter - centerX) * zoom + centerX;
                zoomedNameY = (_parentReference.NameLabelY - centerY) * zoom + centerY;
            }
            else
            {
                zoomedHCenter = _parentReference.HorizontalCenter;
                zoomedNameY = _parentReference.NameLabelY;
            }

            // Health bar: centered on the zoomed center point
            _healthBarPosition = new Vector2(zoomedHCenter - PlateWidth / 2f, zoomedNameY);

            // Damage text: centered on the zoomed center point, floating upward
            if (_damageTextVisible)
            {
                var font = GetDamageFont();
                var textSize = font.MeasureString(_damageText);
                _damageCounterPosition = new Vector2(
                    zoomedHCenter - textSize.Width / 2f,
                    zoomedNameY - _frameOffset * 2 - PlateHeight - 4);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // --- Health Bar (visible for 3 seconds) ---
            if (_barVisible)
            {
                // Background Plate
                var plateRect = new Rectangle(
                    (int)_healthBarPosition.X,
                    (int)_healthBarPosition.Y,
                    PlateWidth,
                    PlateHeight);
                _spriteBatch.Draw(_pixelTexture, plateRect, PlateColor);

                // Health Bar Fill
                var fillWidth = (int)Math.Round(_percentHealth / 100.0 * BarWidth);
                if (fillWidth > 0)
                {
                    var fillRect = new Rectangle(
                        (int)_healthBarPosition.X + PlatePadding,
                        (int)_healthBarPosition.Y + PlatePadding,
                        fillWidth,
                        BarHeight);
                    _spriteBatch.Draw(_pixelTexture, fillRect, BarFillColor);
                }

                // HP Text inside bar (e.g. "%73 HP") with 1px black outline for crispness
                var hpFont = GetHpFont();
                var hpText = $"%{_percentHealth} HP";
                var hpTextSize = hpFont.MeasureString(hpText);
                var hpTextPos = new Vector2(
                    (float)Math.Round(_healthBarPosition.X + (PlateWidth - hpTextSize.Width) / 2f),
                    (float)Math.Round(_healthBarPosition.Y + (PlateHeight - hpTextSize.Height) / 2f));
                DrawOutlinedString(_spriteBatch, hpFont, hpText, hpTextPos, HpTextColor, Color.Black);
            }

            // --- Floating Damage/Heal Number ---
            if (_damageTextVisible && !string.IsNullOrEmpty(_damageText))
            {
                var font = GetDamageFont();
                var baseTextColor = _isMiss ? MissTextColor : (_isHeal ? HealTextColor : DamageTextColor);

                // Fade out over the last 40% of the animation
                var fadeStart = DamageTextDuration * 0.6f;
                var opacity = _frameOffset > fadeStart
                    ? 1f - (_frameOffset - fadeStart) / (DamageTextDuration - fadeStart)
                    : 1f;
                opacity = MathHelper.Clamp(opacity, 0f, 1f);

                var textColor = baseTextColor * opacity;

                // Semi-transparent backdrop behind damage text (fades with text)
                var textSize = font.MeasureString(_damageText);
                var textBackdrop = new Rectangle(
                    (int)_damageCounterPosition.X - 3,
                    (int)_damageCounterPosition.Y - 2,
                    (int)textSize.Width + 6,
                    (int)textSize.Height + 4);
                _spriteBatch.Draw(_pixelTexture, textBackdrop, new Color(0, 0, 0, (int)(120 * opacity)));

                // Text with 1px outline for readability (pixel-rounded position)
                var roundedDmgPos = new Vector2(
                    (float)Math.Round(_damageCounterPosition.X),
                    (float)Math.Round(_damageCounterPosition.Y));
                DrawOutlinedString(_spriteBatch, font, _damageText, roundedDmgPos, textColor, Color.Black * opacity);
            }

            _spriteBatch.End();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spriteBatch.Dispose();
                _pixelTexture?.Dispose();
            }

            base.Dispose(disposing);
        }

        private BitmapFont GetDamageFont()
        {
            return _contentProvider.Fonts[Constants.FontSize09];
        }

        private BitmapFont GetHpFont()
        {
            return _contentProvider.Fonts[Constants.FontSize08];
        }

        private static void DrawOutlinedString(SpriteBatch sb, BitmapFont font, string text, Vector2 pos, Color color, Color outlineColor)
        {
            // 1px outline in 4 cardinal directions
            sb.DrawString(font, text, pos + new Vector2(-1, 0), outlineColor);
            sb.DrawString(font, text, pos + new Vector2(1, 0), outlineColor);
            sb.DrawString(font, text, pos + new Vector2(0, -1), outlineColor);
            sb.DrawString(font, text, pos + new Vector2(0, 1), outlineColor);
            // Main text on top
            sb.DrawString(font, text, pos, color);
        }
    }

    public interface IHealthBarRenderer : IDrawable, IGameComponent, IDisposable
    {
        void SetDamage(Option<int> value, int percentHealth, Action doneCallback = null);

        void SetHealth(int value, int percentHealth, Action doneCallback = null);
    }
}
