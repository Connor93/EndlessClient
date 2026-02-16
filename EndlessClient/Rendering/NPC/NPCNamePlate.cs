using EndlessClient.Content;
using EndlessClient.Rendering.Map;
using EndlessClient.UI.Controls;
using EOLib.IO;
using EOLib.IO.Pub;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.Rendering.NPC
{
    public class NPCNamePlate : DrawableGameComponent
    {
        // Layout constants
        private const int PaddingH = 8;
        private const int PaddingV = 5;
        private const int MinWidth = 50;
        private const int LineSpacing = 2;
        private const int CornerRadius = 3;

        // Colors
        private static readonly Color BackgroundColor = new Color(15, 15, 25, 220);
        private static readonly Color BorderColor = new Color(80, 90, 120, 180);
        private static readonly Color NameColor = new Color(240, 240, 250);
        private static readonly Color AggressiveColor = new Color(220, 80, 80);
        private static readonly Color PassiveColor = new Color(180, 220, 120);
        private static readonly Color VendorColor = new Color(120, 200, 255);
        private static readonly Color QuestColor = new Color(255, 220, 100);
        private static readonly Color DefaultTypeColor = new Color(180, 180, 190);
        private static readonly Color BossColor = new Color(255, 160, 60);

        private readonly IContentProvider _contentProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;

        private BitmapFont _nameFont;
        private BitmapFont _detailFont;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;

        private string _name = string.Empty;
        private NPCType _type;
        private bool _isBoss;

        public Vector2 AnchorPosition { get; set; }

        public bool IsHovered
        {
            get => Visible;
            set => Visible = value;
        }

        public NPCNamePlate(Game game, IContentProvider contentProvider, IClientWindowSizeProvider clientWindowSizeProvider)
            : base(game)
        {
            _contentProvider = contentProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            Visible = false;
            DrawOrder = 40;
        }

        public void UpdateNPCInfo(ENFRecord npcData)
        {
            _name = npcData.Name ?? string.Empty;
            _type = npcData.Type;
            _isBoss = npcData.Boss > 0;
        }

        public override void Initialize()
        {
            _nameFont = _contentProvider.Fonts[Constants.FontSize09];
            _detailFont = _contentProvider.Fonts[Constants.FontSize08];
            _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
            _pixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _spriteBatch == null || _spriteBatch.IsDisposed || string.IsNullOrEmpty(_name))
                return;

            var nameText = _name;
            var typeText = GetTypeLabel();
            var hasType = !string.IsNullOrEmpty(typeText);

            // Measure text sizes
            var nameSize = _nameFont.MeasureString(nameText);
            float typeWidth = 0, typeHeight = 0;
            if (hasType)
            {
                var ts = _detailFont.MeasureString(typeText);
                typeWidth = ts.Width;
                typeHeight = ts.Height;
            }

            // Calculate panel dimensions
            var contentWidth = (int)nameSize.Width;
            if (hasType)
                contentWidth = System.Math.Max(contentWidth, (int)typeWidth);
            contentWidth = System.Math.Max(contentWidth, MinWidth);

            var panelWidth = contentWidth + PaddingH * 2;
            var panelHeight = PaddingV + (int)nameSize.Height;
            if (hasType)
                panelHeight += LineSpacing + (int)typeHeight;
            panelHeight += PaddingV;

            // Position: centered above anchor
            var panelX = (int)(AnchorPosition.X - panelWidth / 2f);
            var panelY = (int)(AnchorPosition.Y - panelHeight - 4);

            // Clamp to screen bounds
            panelX = System.Math.Max(2, System.Math.Min(panelX, _clientWindowSizeProvider.GameWidth - panelWidth - 2));
            panelY = System.Math.Max(2, panelY);

            var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Draw background and border
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, panelRect, BackgroundColor, CornerRadius);
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, panelRect, BorderColor, CornerRadius, 1);

            var textY = panelY + PaddingV;

            // Name (centered, integer-aligned for crisp rendering)
            var nameX = (int)(panelX + (panelWidth - (int)nameSize.Width) / 2f);
            var nameColor = _isBoss ? BossColor : NameColor;
            _spriteBatch.DrawString(_nameFont, nameText, new Vector2(nameX, textY), nameColor);
            textY += (int)nameSize.Height;

            // Type label
            if (hasType)
            {
                // Accent line
                textY += 1;
                var lineWidth = contentWidth - 10;
                if (lineWidth > 0)
                {
                    var lineRect = new Rectangle(panelX + (panelWidth - lineWidth) / 2, textY, lineWidth, 1);
                    _spriteBatch.Draw(_pixel, lineRect, BorderColor * 0.5f);
                }
                textY += 2;

                var typeX = (int)(panelX + (panelWidth - (int)typeWidth) / 2f);
                _spriteBatch.DrawString(_detailFont, typeText, new Vector2(typeX, textY), GetTypeColor());
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private string GetTypeLabel()
        {
            return _type switch
            {
                NPCType.Aggressive => _isBoss ? "Boss" : "Aggressive",
                NPCType.Passive => "Passive",
                NPCType.Shop => "Shop",
                NPCType.Inn => "Inn",
                NPCType.Bank => "Bank",
                NPCType.Barber => "Barber",
                NPCType.Guild => "Guild",
                NPCType.Priest => "Priest",
                NPCType.Law => "Law",
                NPCType.Skills => "Skills",
                NPCType.Quest => "Quest",
                _ => string.Empty
            };
        }

        private Color GetTypeColor()
        {
            return _type switch
            {
                NPCType.Aggressive => _isBoss ? BossColor : AggressiveColor,
                NPCType.Passive => PassiveColor,
                NPCType.Quest => QuestColor,
                NPCType.Shop or NPCType.Inn or NPCType.Bank or NPCType.Barber
                    or NPCType.Guild or NPCType.Priest or NPCType.Law or NPCType.Skills => VendorColor,
                _ => DefaultTypeColor
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spriteBatch?.Dispose();
                _pixel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
