using EOLib.Shared;
using MonoGame.Extended.BitmapFonts;

namespace EndlessClient.Content
{
    /// <summary>
    /// Shared utility for selecting the closest available bitmap font for a given scale factor.
    /// Ensures text scales smoothly with the UI instead of being stretched/grainy.
    /// </summary>
    public static class FontScaleHelper
    {
        // Available font sizes sorted ascending by pixel size
        private static readonly (int pixelSize, string fontKey)[] AvailableFonts = new[]
        {
            (9, Constants.FontSize07),
            (11, Constants.FontSize08),
            (12, Constants.FontSize09),
            (13, Constants.FontSize10),
            (14, Constants.FontSize11),
            (16, Constants.FontSize12),
            (18, Constants.FontSize13),
            (20, Constants.FontSize14),
        };

        /// <summary>
        /// Default base pixel size (11px = FontSize08pt5, the most common UI font).
        /// </summary>
        public const int DefaultBasePx = 11;

        /// <summary>
        /// Selects the closest available bitmap font to <paramref name="basePx"/> × <paramref name="scaleFactor"/>.
        /// </summary>
        /// <param name="contentProvider">Content provider that holds loaded fonts.</param>
        /// <param name="basePx">The pixel size of the font at 1× scale.</param>
        /// <param name="scaleFactor">Current UI scale factor.</param>
        /// <returns>The closest matching BitmapFont.</returns>
        public static BitmapFont GetScaledFont(IContentProvider contentProvider, int basePx, float scaleFactor)
        {
            var targetPx = basePx * scaleFactor;
            var bestKey = AvailableFonts[AvailableFonts.Length - 1].fontKey; // default to largest

            for (int i = 0; i < AvailableFonts.Length; i++)
            {
                if (AvailableFonts[i].pixelSize >= targetPx)
                {
                    // Pick whichever is closer: this one or the previous one
                    if (i > 0)
                    {
                        var diffLower = targetPx - AvailableFonts[i - 1].pixelSize;
                        var diffUpper = AvailableFonts[i].pixelSize - targetPx;
                        bestKey = diffLower <= diffUpper
                            ? AvailableFonts[i - 1].fontKey
                            : AvailableFonts[i].fontKey;
                    }
                    else
                    {
                        bestKey = AvailableFonts[0].fontKey;
                    }
                    break;
                }
            }

            return contentProvider.Fonts[bestKey];
        }

        /// <summary>
        /// Convenience overload using the default base pixel size (11px / FontSize08pt5).
        /// </summary>
        public static BitmapFont GetScaledFont(IContentProvider contentProvider, float scaleFactor)
        {
            return GetScaledFont(contentProvider, DefaultBasePx, scaleFactor);
        }
    }
}
