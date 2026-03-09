namespace EndlessClient.UI.Myra
{
    /// <summary>
    /// Provides FontStashSharp font instances for Myra UI widgets.
    /// Loads a bundled TTF font and renders it at any requested pixel size.
    /// </summary>
    public interface IMyraFontProvider
    {
        /// <summary>
        /// Get a font at the specified pixel size. Results are cached.
        /// </summary>
        FontStashSharp.SpriteFontBase GetFont(int sizeInPixels);

        /// <summary>Small UI text (10px)</summary>
        FontStashSharp.SpriteFontBase Small { get; }

        /// <summary>Normal UI text (12px) — default for most widgets</summary>
        FontStashSharp.SpriteFontBase Normal { get; }

        /// <summary>Large UI text (14px) — dialog content, list items</summary>
        FontStashSharp.SpriteFontBase Large { get; }

        /// <summary>Header text (18px) — window titles, section headers</summary>
        FontStashSharp.SpriteFontBase Header { get; }
    }
}
