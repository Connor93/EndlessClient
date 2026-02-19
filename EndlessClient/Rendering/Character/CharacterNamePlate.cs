using System.Collections.Generic;
using EndlessClient.Content;
using EndlessClient.UI.Controls;
using EOLib.Domain.Character;
using EOLib.Shared;
using Moffat.EndlessOnline.SDK.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.Rendering.Character
{
    /// <summary>
    /// A styled nameplate tooltip that appears above a character on mouse hover.
    /// Displays the player's name, guild tag, and level in a sleek panel.
    /// </summary>
    public class CharacterNamePlate : DrawableGameComponent
    {
        private readonly IContentProvider _contentProvider;
        private readonly IClientWindowSizeProvider _clientWindowSizeProvider;

        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private Texture2D _badgeSheet;
        private BitmapFont _nameFont;
        private BitmapFont _detailFont;

        private string _name = string.Empty;
        private string _guildTag = string.Empty;
        private string _guildName = string.Empty;
        private int _level;
        private AdminLevel _adminLevel;
        private bool _isHovered;
        private string[] _badgeNames = System.Array.Empty<string>();

        private const int BadgeIconSize = 12;
        private const int BadgeContainerSize = 16; // icon + 2px padding each side
        private const int BadgeGap = 3;

        // Map achievement names to spritesheet icon indices (0-based)
        public static readonly Dictionary<string, int> BadgeIconIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Final Boss"] = 0,
            ["Questmaster"] = 1,
            ["Master Crafter"] = 2,
            ["Pet Collector"] = 3,
            ["Arms Dealer"] = 4,
            ["Fashionista"] = 5,
            ["Guardian"] = 6,
            ["Hat Trick"] = 7,
            ["Sole Survivor"] = 8,
            ["Explorer"] = 9,
            ["BAAA"] = 10,
            ["Dragon Warrior"] = 11,
            ["Conqueror Of Hell"] = 12,
        };

        private const int PaddingH = 8;
        private const int PaddingV = 5;
        private const int LineSpacing = 2;
        private const int SectionSpacing = 3;
        private const int CornerRadius = 4;
        private const int MinWidth = 80;

        // Colors
        private static readonly Color BackgroundColor = new Color(15, 15, 25, 220);
        private static readonly Color BorderColor = new Color(100, 120, 180, 200);
        private static readonly Color NameColor = Color.White;
        private static readonly Color GuildColor = new Color(160, 200, 255);
        private static readonly Color LevelColor = new Color(200, 200, 180);
        private static readonly Color AdminColor = new Color(255, 215, 100);
        private static readonly Color GmColor = new Color(255, 100, 100);
        private static readonly Color BadgeContainerColor = new Color(40, 40, 60, 200);
        private static readonly Color BadgeBorderColor = new Color(180, 160, 100, 180);

        /// <summary>
        /// Position to draw the nameplate (top-center anchor point).
        /// </summary>
        public Vector2 AnchorPosition { get; set; }

        /// <summary>
        /// Whether the nameplate is currently visible (mouse hovering over character).
        /// </summary>
        public bool IsHovered
        {
            get => _isHovered;
            set
            {
                _isHovered = value;
                Visible = value;
            }
        }

        public CharacterNamePlate(Game game, IContentProvider contentProvider, IClientWindowSizeProvider clientWindowSizeProvider)
            : base(game)
        {
            _contentProvider = contentProvider;
            _clientWindowSizeProvider = clientWindowSizeProvider;
            Visible = false;
            DrawOrder = 40; // Above health bars (30) and name labels
        }

        public void UpdateCharacterInfo(EOLib.Domain.Character.Character character)
        {
            _name = character.Name ?? string.Empty;
            _guildTag = character.GuildTag ?? string.Empty;
            _guildName = character.GuildName ?? string.Empty;
            _level = character.Stats[CharacterStat.Level];
            _adminLevel = character.AdminLevel;
            _badgeNames = character.BadgeNames ?? System.Array.Empty<string>();
        }

        public override void Initialize()
        {
            _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
            _pixel = new Texture2D(Game.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            if (_contentProvider.Textures.ContainsKey(ContentProvider.IconBadges))
                _badgeSheet = _contentProvider.Textures[ContentProvider.IconBadges];

            _nameFont = _contentProvider.Fonts[Constants.FontSize09];
            _detailFont = _contentProvider.Fonts[Constants.FontSize08];

            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _spriteBatch == null || _spriteBatch.IsDisposed || string.IsNullOrEmpty(_name))
                return;

            // Build display lines
            var nameText = _name;
            var hasGuild = !string.IsNullOrWhiteSpace(_guildTag) && _guildTag.Trim().Length > 0;
            var guildText = hasGuild ? _guildTag.Trim() : string.Empty;
            var levelText = _level > 0 ? $"Lv. {_level}" : string.Empty;
            var isAdmin = _adminLevel > AdminLevel.Player;

            // Measure text sizes
            var nameSize = _nameFont.MeasureString(nameText);
            float guildWidth = 0, guildHeight = 0;
            float levelWidth = 0, levelHeight = 0;
            if (hasGuild)
            {
                var gs = _detailFont.MeasureString(guildText);
                guildWidth = gs.Width;
                guildHeight = gs.Height;
            }
            if (!string.IsNullOrEmpty(levelText))
            {
                var ls = _detailFont.MeasureString(levelText);
                levelWidth = ls.Width;
                levelHeight = ls.Height;
            }

            // Calculate panel dimensions
            var contentWidth = (int)nameSize.Width;
            if (hasGuild)
                contentWidth = System.Math.Max(contentWidth, (int)guildWidth);
            if (!string.IsNullOrEmpty(levelText))
                contentWidth = System.Math.Max(contentWidth, (int)levelWidth);
            contentWidth = System.Math.Max(contentWidth, MinWidth);
            if (_badgeNames.Length > 0)
            {
                var badgeWidth = _badgeNames.Length * BadgeContainerSize + (_badgeNames.Length - 1) * BadgeGap;
                contentWidth = System.Math.Max(contentWidth, badgeWidth);
            }

            var panelWidth = contentWidth + PaddingH * 2;
            var panelHeight = PaddingV + (int)nameSize.Height;
            if (hasGuild)
                panelHeight += LineSpacing + (int)guildHeight;
            if (!string.IsNullOrEmpty(levelText))
                panelHeight += SectionSpacing + (int)levelHeight;

            // Badge icons height
            if (_badgeNames.Length > 0)
                panelHeight += SectionSpacing + BadgeContainerSize;

            panelHeight += PaddingV;

            // Position: centered above anchor, with a small gap
            var panelX = (int)(AnchorPosition.X - panelWidth / 2f);
            var panelY = (int)(AnchorPosition.Y - panelHeight - 4);

            // Clamp to screen bounds
            panelX = System.Math.Max(2, System.Math.Min(panelX, _clientWindowSizeProvider.GameWidth - panelWidth - 2));
            panelY = System.Math.Max(2, panelY);

            var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Draw background
            DrawingPrimitives.DrawRoundedRect(_spriteBatch, panelRect, BackgroundColor, CornerRadius);

            // Draw border
            DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, panelRect, BorderColor, CornerRadius, 1);

            // Draw accent line under the name
            var textY = panelY + PaddingV;

            // Name (centered, integer-aligned for crisp BitmapFont rendering)
            var nameX = (int)(panelX + (panelWidth - (int)nameSize.Width) / 2f);
            var nameColor = isAdmin ? (_adminLevel >= AdminLevel.HighGameMaster ? GmColor : AdminColor) : NameColor;
            _spriteBatch.DrawString(_nameFont, nameText, new Vector2(nameX, textY), nameColor);
            textY += (int)nameSize.Height;

            // Accent line
            if (hasGuild || !string.IsNullOrEmpty(levelText))
            {
                textY += 1;
                var lineWidth = contentWidth - 10;
                if (lineWidth > 0)
                {
                    var lineRect = new Rectangle(panelX + (panelWidth - lineWidth) / 2, textY, lineWidth, 1);
                    _spriteBatch.Draw(_pixel, lineRect, BorderColor * 0.5f);
                }
                textY += 2;
            }

            // Guild tag
            if (hasGuild)
            {
                var guildX = (int)(panelX + (panelWidth - (int)guildWidth) / 2f);
                _spriteBatch.DrawString(_detailFont, guildText, new Vector2(guildX, textY), GuildColor);
                textY += (int)guildHeight + LineSpacing;
            }

            // Level
            if (!string.IsNullOrEmpty(levelText))
            {
                var levelX = (int)(panelX + (panelWidth - (int)levelWidth) / 2f);
                _spriteBatch.DrawString(_detailFont, levelText, new Vector2(levelX, textY), LevelColor);
                textY += (int)levelHeight + LineSpacing;
            }

            // Badge icons
            if (_badgeNames.Length > 0)
            {
                textY += SectionSpacing;
                var totalWidth = _badgeNames.Length * BadgeContainerSize + (_badgeNames.Length - 1) * BadgeGap;
                var startX = panelX + (panelWidth - totalWidth) / 2;

                for (int i = 0; i < _badgeNames.Length; i++)
                {
                    var bx = startX + i * (BadgeContainerSize + BadgeGap);

                    // Draw container background + border
                    var containerRect = new Rectangle(bx, textY, BadgeContainerSize, BadgeContainerSize);
                    DrawingPrimitives.DrawRoundedRect(_spriteBatch, containerRect, BadgeContainerColor, 3);
                    DrawingPrimitives.DrawRoundedRectBorder(_spriteBatch, containerRect, BadgeBorderColor, 3, 1);

                    // Draw icon from spritesheet
                    if (_badgeSheet != null && BadgeIconIndex.TryGetValue(_badgeNames[i], out var iconIdx))
                    {
                        var srcRect = new Rectangle(iconIdx * BadgeIconSize, 0, BadgeIconSize, BadgeIconSize);
                        var dstRect = new Rectangle(bx + 2, textY + 2, BadgeIconSize, BadgeIconSize);
                        _spriteBatch.Draw(_badgeSheet, dstRect, srcRect, Color.White);
                    }
                    else
                    {
                        // Fallback: draw badge name text if icon not found
                        var abbr = _badgeNames[i].Length > 3 ? _badgeNames[i].Substring(0, 3) : _badgeNames[i];
                        _spriteBatch.DrawString(_detailFont, abbr, new Vector2(bx + 1, textY + 2), Color.Yellow);
                    }
                }
            }

            _spriteBatch.End();

            base.Draw(gameTime);
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
