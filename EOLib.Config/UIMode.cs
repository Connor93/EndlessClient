namespace EOLib.Config
{
    /// <summary>
    /// Determines whether UI elements use GFX textures or code-drawn rendering
    /// </summary>
    public enum UIMode
    {
        /// <summary>
        /// Use traditional GFX texture-based UI (default)
        /// </summary>
        Gfx,

        /// <summary>
        /// Use procedurally-drawn code-based UI
        /// </summary>
        Code
    }
}
