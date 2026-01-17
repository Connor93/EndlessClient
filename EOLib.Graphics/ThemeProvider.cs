using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutomaticTypeMapper;
using EOLib.Shared;

namespace EOLib.Graphics
{
    [AutoMappedType(IsSingleton = true)]
    public class ThemeProvider : IThemeProvider
    {
        private string _activeThemeName;
        private readonly Dictionary<GFXTypes, string> _themeFilePaths;

        public string ActiveThemeName => _activeThemeName;
        public bool HasActiveTheme => !string.IsNullOrEmpty(_activeThemeName);

        public ThemeProvider()
        {
            _themeFilePaths = new Dictionary<GFXTypes, string>();
        }

        public void SetActiveTheme(string themeName)
        {
            _themeFilePaths.Clear();
            _activeThemeName = null;

            if (string.IsNullOrEmpty(themeName))
                return;

            var themesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gfx", "themes");
            if (!Directory.Exists(themesDir))
                return;

            // Look for theme files matching the pattern: {themeName}001.egf, {themeName}002.egf
            var theme001 = Path.Combine(themesDir, $"{themeName}001.egf");
            var theme002 = Path.Combine(themesDir, $"{themeName}002.egf");

            if (File.Exists(theme001))
                _themeFilePaths[GFXTypes.PreLoginUI] = theme001;

            if (File.Exists(theme002))
                _themeFilePaths[GFXTypes.PostLoginUI] = theme002;

            if (_themeFilePaths.Count > 0)
                _activeThemeName = themeName;
        }

        public bool TryGetThemeFilePath(GFXTypes gfxType, out string filePath)
        {
            return _themeFilePaths.TryGetValue(gfxType, out filePath);
        }

        public IEnumerable<string> GetAvailableThemes()
        {
            var themesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gfx", "themes");
            if (!Directory.Exists(themesDir))
                return Enumerable.Empty<string>();

            return Directory.GetFiles(themesDir, "*000.ini")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Select(f => f.Substring(0, f.Length - 3)) // Remove "000" suffix
                .Where(f => f.Length >= 4);
        }
    }

    public interface IThemeProvider
    {
        string ActiveThemeName { get; }
        bool HasActiveTheme { get; }
        void SetActiveTheme(string themeName);
        bool TryGetThemeFilePath(GFXTypes gfxType, out string filePath);
        IEnumerable<string> GetAvailableThemes();
    }
}
