using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EOLib.Config;

namespace SettingsEditor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly List<Resolution> Resolutions = new()
    {
        new Resolution(800, 600),
        new Resolution(1024, 768),
        new Resolution(1200, 800),
        new Resolution(1280, 720),
        new Resolution(1366, 768),
        new Resolution(1440, 900),
        new Resolution(1600, 900),
        new Resolution(1920, 1080),
        new Resolution(2560, 1440),
        new Resolution(3840, 2160),
    };

    private static readonly List<string> ThemeOptions = new()
    {
        "everendless",
    };

    private static readonly List<string> UIModeOptions = new() { "Gfx", "Code" };
    private static readonly List<string> UIStyleOptions = new() { "Glass", "Flat", "Classic", "Parchment", "DarkParchment" };
    private static readonly List<string> LanguageOptions = new() { "English", "Dutch", "Swedish", "Portuguese" };

    private readonly string _configPath;

    // Connection
    private string _host = "127.0.0.1";
    private int _port = 8078;

    // Version
    private int _versionMajor;
    private int _versionMinor;
    private int _versionClient = 28;

    // Settings
    private bool _musicEnabled;
    private int _soundVolume;
    private bool _showBalloons = true;
    private bool _showShadows = true;

    // Custom
    private int _selectedResolutionIndex = 2; // 1200x800 default
    private int _accountCreateTimeout = 2000;
    private bool _showTransition;
    private int _selectedThemeIndex;
    private bool _npcGhosting;
    private bool _wasdMovement;
    private int _selectedUIModeIndex;
    private int _selectedUIStyleIndex;
    private float _mapZoom = 1.0f;
    private bool _scrollWheelZoom;
    private bool _autoLoot = true;
    private int _maxFPS = 60;

    // Language
    private int _selectedLanguageIndex;

    // Chat
    private bool _curseFilter;
    private bool _strictFilter;
    private bool _logChat;
    private bool _hearWhispers = true;
    private bool _interaction = true;

    // UI State
    private string _statusText = string.Empty;

    public MainViewModel()
    {
        _configPath = Program.ConfigPathOverride
            ?? Path.Combine(AppContext.BaseDirectory, "config", "settings.ini");
        SaveCommand = new RelayCommand(Save);
        LoadSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Collections for combo boxes
    public List<Resolution> ResolutionList => Resolutions;
    public List<string> ThemeList => ThemeOptions;
    public List<string> UIModeList => UIModeOptions;
    public List<string> UIStyleList => UIStyleOptions;
    public List<string> LanguageList => LanguageOptions;

    // Connection
    public string Host
    {
        get => _host;
        set => SetField(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetField(ref _port, Math.Clamp(value, 1, 65535));
    }

    // Version
    public int VersionMajor
    {
        get => _versionMajor;
        set => SetField(ref _versionMajor, Math.Clamp(value, 0, 255));
    }

    public int VersionMinor
    {
        get => _versionMinor;
        set => SetField(ref _versionMinor, Math.Clamp(value, 0, 255));
    }

    public int VersionClient
    {
        get => _versionClient;
        set => SetField(ref _versionClient, Math.Clamp(value, 0, 255));
    }

    // Settings
    public bool MusicEnabled
    {
        get => _musicEnabled;
        set => SetField(ref _musicEnabled, value);
    }

    public int SoundVolume
    {
        get => _soundVolume;
        set => SetField(ref _soundVolume, Math.Clamp(value, 0, 100));
    }

    public bool ShowBalloons
    {
        get => _showBalloons;
        set => SetField(ref _showBalloons, value);
    }

    public bool ShowShadows
    {
        get => _showShadows;
        set => SetField(ref _showShadows, value);
    }

    // Custom
    public int SelectedResolutionIndex
    {
        get => _selectedResolutionIndex;
        set => SetField(ref _selectedResolutionIndex, value);
    }

    public int AccountCreateTimeout
    {
        get => _accountCreateTimeout;
        set => SetField(ref _accountCreateTimeout, Math.Max(500, value));
    }

    public bool ShowTransition
    {
        get => _showTransition;
        set => SetField(ref _showTransition, value);
    }

    public int SelectedThemeIndex
    {
        get => _selectedThemeIndex;
        set => SetField(ref _selectedThemeIndex, value);
    }

    public bool NPCGhosting
    {
        get => _npcGhosting;
        set => SetField(ref _npcGhosting, value);
    }

    public bool WASDMovement
    {
        get => _wasdMovement;
        set => SetField(ref _wasdMovement, value);
    }

    public int SelectedUIModeIndex
    {
        get => _selectedUIModeIndex;
        set => SetField(ref _selectedUIModeIndex, value);
    }

    public int SelectedUIStyleIndex
    {
        get => _selectedUIStyleIndex;
        set => SetField(ref _selectedUIStyleIndex, value);
    }

    public float MapZoom
    {
        get => _mapZoom;
        set => SetField(ref _mapZoom, Math.Clamp(value, 1.0f, 2.0f));
    }

    public bool ScrollWheelZoom
    {
        get => _scrollWheelZoom;
        set => SetField(ref _scrollWheelZoom, value);
    }

    public bool AutoLoot
    {
        get => _autoLoot;
        set => SetField(ref _autoLoot, value);
    }

    public int MaxFPS
    {
        get => _maxFPS;
        set => SetField(ref _maxFPS, Math.Clamp(value, 0, 240));
    }

    // Language
    public int SelectedLanguageIndex
    {
        get => _selectedLanguageIndex;
        set => SetField(ref _selectedLanguageIndex, value);
    }

    // Chat
    public bool CurseFilter
    {
        get => _curseFilter;
        set => SetField(ref _curseFilter, value);
    }

    public bool StrictFilter
    {
        get => _strictFilter;
        set => SetField(ref _strictFilter, value);
    }

    public bool LogChat
    {
        get => _logChat;
        set => SetField(ref _logChat, value);
    }

    public bool HearWhispers
    {
        get => _hearWhispers;
        set => SetField(ref _hearWhispers, value);
    }

    public bool Interaction
    {
        get => _interaction;
        set => SetField(ref _interaction, value);
    }

    // UI State
    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public ICommand SaveCommand { get; }

    private void LoadSettings()
    {
        if (!File.Exists(_configPath))
        {
            StatusText = "config/settings.ini not found — using defaults";
            return;
        }

        try
        {
            var reader = new IniReader(_configPath);
            if (!reader.Load())
            {
                StatusText = "Failed to parse config file — using defaults";
                return;
            }

            // Connection
            if (reader.GetValue(ConfigStrings.Connection, ConfigStrings.Host, out string host))
                _host = host;
            if (reader.GetValue(ConfigStrings.Connection, ConfigStrings.Port, out int port))
                _port = port;

            // Version
            if (reader.GetValue(ConfigStrings.Version, ConfigStrings.Major, out int major))
                _versionMajor = major;
            if (reader.GetValue(ConfigStrings.Version, ConfigStrings.Minor, out int minor))
                _versionMinor = minor;
            if (reader.GetValue(ConfigStrings.Version, ConfigStrings.Client, out int client))
                _versionClient = client;

            // Settings
            if (reader.GetValue(ConfigStrings.Settings, ConfigStrings.Music, out bool music))
                _musicEnabled = music;

            if (reader.GetValue(ConfigStrings.Settings, ConfigStrings.Sound, out string soundStr))
            {
                if (int.TryParse(soundStr, out var soundInt))
                    _soundVolume = Math.Clamp(soundInt, 0, 100);
                else if (soundStr.Equals("on", StringComparison.OrdinalIgnoreCase))
                    _soundVolume = 100;
                else
                    _soundVolume = 0;
            }

            if (reader.GetValue(ConfigStrings.Settings, ConfigStrings.ShowBaloons, out bool balloons))
                _showBalloons = balloons;
            if (reader.GetValue(ConfigStrings.Settings, ConfigStrings.ShowShadows, out bool shadows))
                _showShadows = shadows;

            // Custom - Resolution
            int width = 1200, height = 800;
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.InGameWidth, out int w))
                width = w;
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.InGameHeight, out int h))
                height = h;
            _selectedResolutionIndex = FindResolutionIndex(width, height);

            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.AccountCreateTimeout, out int timeout))
                _accountCreateTimeout = timeout;
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.ShowTransition, out bool transition))
                _showTransition = transition;

            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.Theme, out string theme))
            {
                var idx = ThemeOptions.FindIndex(t => t.Equals(theme, StringComparison.OrdinalIgnoreCase));
                _selectedThemeIndex = idx >= 0 ? idx : 0;
            }

            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.NPCGhosting, out bool ghosting))
                _npcGhosting = ghosting;
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.WASDMovement, out bool wasd))
                _wasdMovement = wasd;
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.AutoLoot, out bool autoLoot))
                _autoLoot = autoLoot;

            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.UIMode, out string uiModeStr))
            {
                var idx = UIModeOptions.FindIndex(m => m.Equals(uiModeStr, StringComparison.OrdinalIgnoreCase));
                _selectedUIModeIndex = idx >= 0 ? idx : 0;
            }

            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.UIStyle, out string uiStyleStr))
            {
                var idx = UIStyleOptions.FindIndex(s => s.Equals(uiStyleStr, StringComparison.OrdinalIgnoreCase));
                _selectedUIStyleIndex = idx >= 0 ? idx : 0;
            }

            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.MapZoom, out float zoom))
                _mapZoom = Math.Clamp(zoom, 1.0f, 2.0f);
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.ScrollWheelZoom, out bool scrollZoom))
                _scrollWheelZoom = scrollZoom;
            if (reader.GetValue(ConfigStrings.Custom, ConfigStrings.MaxFPS, out int fps))
                _maxFPS = Math.Clamp(fps, 0, 240);

            // Language
            if (reader.GetValue(ConfigStrings.LANGUAGE, ConfigStrings.Language, out int lang))
                _selectedLanguageIndex = Math.Clamp(lang, 0, LanguageOptions.Count - 1);

            // Chat
            if (reader.GetValue(ConfigStrings.Chat, ConfigStrings.Filter, out bool filter))
                _curseFilter = filter;
            if (reader.GetValue(ConfigStrings.Chat, ConfigStrings.FilterAll, out bool filterAll))
                _strictFilter = filterAll;
            if (reader.GetValue(ConfigStrings.Chat, ConfigStrings.LogChat, out bool logChat))
                _logChat = logChat;
            if (reader.GetValue(ConfigStrings.Chat, ConfigStrings.HearWhisper, out bool whisper))
                _hearWhispers = whisper;
            if (reader.GetValue(ConfigStrings.Chat, ConfigStrings.Interaction, out bool interaction))
                _interaction = interaction;

            StatusText = "Settings loaded successfully";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading settings: {ex.Message}";
        }
    }

    private void Save()
    {
        try
        {
            var reader = new IniReader(_configPath);
            reader.Load();

            var resolution = _selectedResolutionIndex >= 0 && _selectedResolutionIndex < Resolutions.Count
                ? Resolutions[_selectedResolutionIndex]
                : Resolutions[2];

            // Connection
            SetIniValue(reader, ConfigStrings.Connection, ConfigStrings.Host, Host);
            SetIniValue(reader, ConfigStrings.Connection, ConfigStrings.Port, Port.ToString());

            // Version
            SetIniValue(reader, ConfigStrings.Version, ConfigStrings.Major, VersionMajor.ToString());
            SetIniValue(reader, ConfigStrings.Version, ConfigStrings.Minor, VersionMinor.ToString());
            SetIniValue(reader, ConfigStrings.Version, ConfigStrings.Client, VersionClient.ToString());

            // Settings
            SetIniValue(reader, ConfigStrings.Settings, ConfigStrings.Music, BoolToOnOff(MusicEnabled));
            SetIniValue(reader, ConfigStrings.Settings, ConfigStrings.Sound, SoundVolume.ToString());
            SetIniValue(reader, ConfigStrings.Settings, ConfigStrings.ShowBaloons, BoolToOnOff(ShowBalloons));
            SetIniValue(reader, ConfigStrings.Settings, ConfigStrings.ShowShadows, BoolToStr(ShowShadows));

            // Custom
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.InGameWidth, resolution.Width.ToString());
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.InGameHeight, resolution.Height.ToString());
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.AccountCreateTimeout, AccountCreateTimeout.ToString());
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.ShowTransition, BoolToStr(ShowTransition));

            var themeValue = _selectedThemeIndex >= 0 && _selectedThemeIndex < ThemeOptions.Count
                ? ThemeOptions[_selectedThemeIndex]
                : ThemeOptions[0];
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.Theme, themeValue);

            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.NPCGhosting, BoolToStr(NPCGhosting));
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.WASDMovement, BoolToStr(WASDMovement));
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.AutoLoot, BoolToOnOff(AutoLoot));

            var uiModeValue = _selectedUIModeIndex >= 0 && _selectedUIModeIndex < UIModeOptions.Count
                ? UIModeOptions[_selectedUIModeIndex].ToLowerInvariant()
                : "gfx";
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.UIMode, uiModeValue);

            var uiStyleValue = _selectedUIStyleIndex >= 0 && _selectedUIStyleIndex < UIStyleOptions.Count
                ? UIStyleOptions[_selectedUIStyleIndex].ToLowerInvariant()
                : "glass";
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.UIStyle, uiStyleValue);

            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.MapZoom, MapZoom.ToString("F2", CultureInfo.InvariantCulture));
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.ScrollWheelZoom, BoolToOnOff(ScrollWheelZoom));
            SetIniValue(reader, ConfigStrings.Custom, ConfigStrings.MaxFPS, MaxFPS.ToString());

            // Language
            SetIniValue(reader, ConfigStrings.LANGUAGE, ConfigStrings.Language, SelectedLanguageIndex.ToString());

            // Chat
            SetIniValue(reader, ConfigStrings.Chat, ConfigStrings.Filter, BoolToOnOff(CurseFilter));
            SetIniValue(reader, ConfigStrings.Chat, ConfigStrings.FilterAll, BoolToOnOff(StrictFilter));
            SetIniValue(reader, ConfigStrings.Chat, ConfigStrings.LogChat, BoolToOnOff(LogChat));
            SetIniValue(reader, ConfigStrings.Chat, ConfigStrings.HearWhisper, BoolToOnOff(HearWhispers));
            SetIniValue(reader, ConfigStrings.Chat, ConfigStrings.Interaction, BoolToOnOff(Interaction));

            reader.Save();
            StatusText = "Settings saved successfully!";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
        }
    }

    private static int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < Resolutions.Count; i++)
        {
            if (Resolutions[i].Width == width && Resolutions[i].Height == height)
                return i;
        }
        return 2; // default to 1200x800
    }

    private static void SetIniValue(IniReader reader, string section, string key, string value)
    {
        if (!reader.Sections.ContainsKey(section))
            reader.Sections.Add(section, new System.Collections.Generic.SortedList<string, string>());
        reader.Sections[section][key] = value;
    }

    private static string BoolToOnOff(bool value) => value ? "on" : "off";
    private static string BoolToStr(bool value) => value ? "true" : "false";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public record Resolution(int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height}";
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
