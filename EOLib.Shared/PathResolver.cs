using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EOLib.Shared
{
    public static class PathResolver
    {
        public const string LocalFilesRoot = ".endlessclient";
        public static string ResourcesRoot { get; } = Path.Combine("Contents", "Resources");

        public static string GetPath(string inputPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var bundlePath = Path.Combine(ResourcesRoot, inputPath);
                // When running in app bundle, Contents/Resources exists
                // When running in development (dotnet run), fall back to direct path
                if (Directory.Exists(Path.GetDirectoryName(bundlePath)) || File.Exists(bundlePath))
                {
                    return bundlePath;
                }
                // Fallback: check if path exists directly (development mode)
                return inputPath;
            }
            else
            {
                return inputPath;
            }
        }

        public static string GetModifiablePath(string inputPath)
        {
            // Always use the local path (from ClientAssets via build output)
            return inputPath;
        }
    }
}
