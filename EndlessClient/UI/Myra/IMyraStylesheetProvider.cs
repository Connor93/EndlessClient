using Myra.Graphics2D.UI.Styles;

namespace EndlessClient.UI.Myra
{
    /// <summary>
    /// Provides a Myra Stylesheet built from the current IUIStyleProvider colors.
    /// Supports hot-swapping themes at runtime — changing the style provider and
    /// calling Rebuild() will update all Myra widgets immediately.
    /// </summary>
    public interface IMyraStylesheetProvider
    {
        /// <summary>
        /// Get the current Myra stylesheet. Returns the cached instance
        /// unless Rebuild() has been called.
        /// </summary>
        Stylesheet Stylesheet { get; }

        /// <summary>
        /// Rebuild the stylesheet from the current IUIStyleProvider.
        /// Call this after switching themes to update all Myra widgets.
        /// </summary>
        void Rebuild();

        /// <summary>
        /// Build and apply the stylesheet as the current global stylesheet.
        /// </summary>
        void Apply();
    }
}
