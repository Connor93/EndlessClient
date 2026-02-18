using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutomaticTypeMapper;
using EndlessClient.Audio;
using EOLib.Shared;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.Content
{
    public interface IContentProvider
    {
        IReadOnlyDictionary<string, Texture2D> Textures { get; }

        IReadOnlyDictionary<string, BitmapFont> Fonts { get; }

        IReadOnlyDictionary<SoundEffectID, SoundEffect> SFX { get; }

        IReadOnlyList<SoundEffect> HarpNotes { get; }

        IReadOnlyList<SoundEffect> GuitarNotes { get; }

        void SetContentManager(ContentManager content);

        void Load();
    }

    [AutoMappedType(IsSingleton = true)]
    public class ContentProvider : IContentProvider
    {
        private readonly Dictionary<string, Texture2D> _textures;
        private readonly Dictionary<string, BitmapFont> _fonts;
        private readonly Dictionary<SoundEffectID, SoundEffect> _sfx;
        private readonly List<SoundEffect> _harpNotes;
        private readonly List<SoundEffect> _guitarNotes;

        private ContentManager _content;

        public const string Cursor = "cursor";

        public const string TBBack = "tbBack";
        public const string TBLeft = "tbLeft";
        public const string TBRight = "tbRight";

        public const string ChatTL = @"ChatBubble/TL";
        public const string ChatTM = @"ChatBubble/TM";
        public const string ChatTR = @"ChatBubble/TR";
        public const string ChatML = @"ChatBubble/ML";
        public const string ChatMM = @"ChatBubble/MM";
        public const string ChatMR = @"ChatBubble/MR";
        public const string ChatRL = @"ChatBubble/RL";
        public const string ChatRM = @"ChatBubble/RM";
        public const string ChatRR = @"ChatBubble/RR";
        public const string ChatNUB = @"ChatBubble/NUB";

        public const string HPOutline = @"Party/hp-outline";
        public const string HPRed = @"Party/hp-red";
        public const string HPYellow = @"Party/hp-yellow";
        public const string HPGreen = @"Party/hp-green";

        public const string IconMap = @"HudIcons/icon_map";
        public const string IconInventory = @"HudIcons/icon_inventory";
        public const string IconSpells = @"HudIcons/icon_spells";
        public const string IconPassive = @"HudIcons/icon_passive";
        public const string IconStats = @"HudIcons/icon_stats";
        public const string IconEquip = @"HudIcons/icon_equip";
        public const string IconMacro = @"HudIcons/icon_macro";
        public const string IconOnline = @"HudIcons/icon_online";
        public const string IconParty = @"HudIcons/icon_party";
        public const string IconConfig = @"HudIcons/icon_config";
        public const string IconExp = @"HudIcons/icon_exp";
        public const string IconQuests = @"HudIcons/icon_quests";
        public const string IconBounties = @"HudIcons/icon_bounties";
        public const string IconGuildInfo = @"HudIcons/icon_guild_info";
        public const string IconGuildPanel = @"HudIcons/icon_guild_panel";
        public const string IconAchievements = @"HudIcons/icon_achievements";

        public IReadOnlyDictionary<string, Texture2D> Textures => _textures;

        public IReadOnlyDictionary<string, BitmapFont> Fonts => _fonts;

        public IReadOnlyDictionary<SoundEffectID, SoundEffect> SFX => _sfx;

        public IReadOnlyList<SoundEffect> HarpNotes => _harpNotes;

        public IReadOnlyList<SoundEffect> GuitarNotes => _guitarNotes;

        public ContentProvider()
        {
            _textures = new Dictionary<string, Texture2D>();
            _fonts = new Dictionary<string, BitmapFont>();
            _sfx = new Dictionary<SoundEffectID, SoundEffect>();
            _harpNotes = new List<SoundEffect>();
            _guitarNotes = new List<SoundEffect>();
        }

        public void SetContentManager(ContentManager content)
        {
            _content = content;
        }

        public void Load()
        {
            RefreshTextures();
            RefreshFonts();
            LoadSFX();
            LoadHarp();
            LoadGuitar();
        }

        private void RefreshTextures()
        {
            if (_content == null)
                return;

            _textures[Cursor] = _content.Load<Texture2D>(Cursor);

            _textures[TBBack] = _content.Load<Texture2D>(TBBack);
            _textures[TBLeft] = _content.Load<Texture2D>(TBLeft);
            _textures[TBRight] = _content.Load<Texture2D>(TBRight);

            _textures[ChatTL] = _content.Load<Texture2D>(ChatTL);
            _textures[ChatTM] = _content.Load<Texture2D>(ChatTM);
            _textures[ChatTR] = _content.Load<Texture2D>(ChatTR);
            _textures[ChatML] = _content.Load<Texture2D>(ChatML);
            _textures[ChatMM] = _content.Load<Texture2D>(ChatMM);
            _textures[ChatMR] = _content.Load<Texture2D>(ChatMR);
            _textures[ChatRL] = _content.Load<Texture2D>(ChatRL);
            _textures[ChatRM] = _content.Load<Texture2D>(ChatRM);
            _textures[ChatRR] = _content.Load<Texture2D>(ChatRR);
            _textures[ChatNUB] = _content.Load<Texture2D>(ChatNUB);

            _textures[HPOutline] = _content.Load<Texture2D>(HPOutline);
            _textures[HPRed] = _content.Load<Texture2D>(HPRed);
            _textures[HPYellow] = _content.Load<Texture2D>(HPYellow);
            _textures[HPGreen] = _content.Load<Texture2D>(HPGreen);

            _textures[IconMap] = _content.Load<Texture2D>(IconMap);
            _textures[IconInventory] = _content.Load<Texture2D>(IconInventory);
            _textures[IconSpells] = _content.Load<Texture2D>(IconSpells);
            _textures[IconPassive] = _content.Load<Texture2D>(IconPassive);
            _textures[IconStats] = _content.Load<Texture2D>(IconStats);
            _textures[IconEquip] = _content.Load<Texture2D>(IconEquip);
            _textures[IconMacro] = _content.Load<Texture2D>(IconMacro);
            _textures[IconOnline] = _content.Load<Texture2D>(IconOnline);
            _textures[IconParty] = _content.Load<Texture2D>(IconParty);
            _textures[IconConfig] = _content.Load<Texture2D>(IconConfig);
            _textures[IconExp] = _content.Load<Texture2D>(IconExp);
            _textures[IconQuests] = _content.Load<Texture2D>(IconQuests);
            _textures[IconBounties] = _content.Load<Texture2D>(IconBounties);
            _textures[IconGuildInfo] = _content.Load<Texture2D>(IconGuildInfo);
            _textures[IconGuildPanel] = _content.Load<Texture2D>(IconGuildPanel);
            _textures[IconAchievements] = _content.Load<Texture2D>(IconAchievements);
        }

        private void RefreshFonts()
        {
            _fonts[Constants.FontSize08] = _content.Load<BitmapFont>(Constants.FontSize08);
            _fonts[Constants.FontSize08pt5] = _content.Load<BitmapFont>(Constants.FontSize08pt5);
            _fonts[Constants.FontSize09] = _content.Load<BitmapFont>(Constants.FontSize09);
            _fonts[Constants.FontSize10] = _content.Load<BitmapFont>(Constants.FontSize10);
            _fonts[Constants.FontSize11] = _content.Load<BitmapFont>(Constants.FontSize11);
            _fonts[Constants.FontSize12] = _content.Load<BitmapFont>(Constants.FontSize12);
            _fonts[Constants.FontSize13] = _content.Load<BitmapFont>(Constants.FontSize13);
            _fonts[Constants.FontSize14] = _content.Load<BitmapFont>(Constants.FontSize14);
        }

        private void LoadSFX()
        {
            var id = (SoundEffectID)0;
            foreach (var sfxFile in GetSoundEffects("sfx???.wav"))
                _sfx[id++] = sfxFile;
            if (_sfx.Count < 81)
                throw new FileNotFoundException($"Unexpected number of SFX (Expected 81, Found {_sfx.Count})");
        }

        private void LoadHarp()
        {
            _harpNotes.AddRange(GetSoundEffects("har*.wav"));
            if (_harpNotes.Count != 36)
                throw new FileNotFoundException($"Unexpected number of harp SFX (Expected 36, Found {_harpNotes.Count})");
        }

        private void LoadGuitar()
        {
            _guitarNotes.AddRange(GetSoundEffects("gui*.wav"));
            if (_guitarNotes.Count != 36)
                throw new FileNotFoundException($"Unexpected number of guitar SFX (Expected 36, Found {_guitarNotes.Count})");
        }

        private static IEnumerable<SoundEffect> GetSoundEffects(string filter)
        {
            if (!Directory.Exists(Constants.SfxDirectory))
                throw new DirectoryNotFoundException($"SFX directory not found: {Constants.SfxDirectory}");

            var sfxFiles = Directory.GetFiles(Constants.SfxDirectory, filter).ToList();
            sfxFiles.Sort();

            foreach (var file in sfxFiles)
            {
                using var wavStream = WAVFileValidator.GetStreamWithCorrectLengthHeader(file);
                yield return SoundEffect.FromStream(wavStream);
            }
        }
    }
}
