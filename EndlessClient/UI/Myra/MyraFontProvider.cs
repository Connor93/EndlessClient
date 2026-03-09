using System.IO;
using AutomaticTypeMapper;
using FontStashSharp;

namespace EndlessClient.UI.Myra
{
    /// <summary>
    /// Loads a bundled TTF font (Inter) via FontStashSharp and provides
    /// SpriteFontBase instances at any requested pixel size.
    /// Thread-safe — FontSystem handles concurrent access.
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class MyraFontProvider : IMyraFontProvider
    {
        private const string FontPath = "Fonts/Inter-Regular.ttf";

        private readonly FontSystem _fontSystem;

        public MyraFontProvider()
        {
            _fontSystem = new FontSystem();

            // Load the TTF font from the output directory (copied from ClientAssets)
            var fontBytes = File.ReadAllBytes(Path.Combine("Fonts", "Inter-Regular.ttf"));
            _fontSystem.AddFont(fontBytes);
        }

        public SpriteFontBase GetFont(int sizeInPixels)
        {
            return _fontSystem.GetFont(sizeInPixels);
        }

        public SpriteFontBase Small => GetFont(10);
        public SpriteFontBase Normal => GetFont(12);
        public SpriteFontBase Large => GetFont(14);
        public SpriteFontBase Header => GetFont(18);
    }
}
