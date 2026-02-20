using System;
using System.Linq;
using Avalonia;

namespace SettingsEditor;

class Program
{
    internal static string? ConfigPathOverride { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Parse --config <path> argument (used by macOS .app launcher)
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config")
            {
                ConfigPathOverride = args[i + 1];
                break;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
