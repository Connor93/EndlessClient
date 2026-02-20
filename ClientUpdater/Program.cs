using System;
using Avalonia;

namespace ClientUpdater;

class Program
{
    internal static string? GamePathOverride { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Parse --path <dir> argument for game directory override
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--path")
            {
                GamePathOverride = args[i + 1];
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
