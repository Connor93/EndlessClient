using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EOLib.Config;

namespace ClientUpdater.ViewModels;

public class UpdaterViewModel : INotifyPropertyChanged
{
    private const string GitHubApiUrl = "https://api.github.com/repos/Connor93/EndlessClient/releases/latest";
    private const string VersionFileName = "version.txt";

    private static readonly HttpClient s_httpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "EndlessClient-Updater" },
            { "Accept", "application/vnd.github.v3+json" }
        },
        Timeout = TimeSpan.FromMinutes(10)
    };

    private string _currentVersion = "Unknown";
    private string _latestVersion = "";
    private string _statusText = "Ready";
    private string _logText = "";
    private double _downloadProgress;
    private double _extractionProgress;
    private string _downloadProgressText = "";
    private string _extractionProgressText = "";
    private bool _isCheckingForUpdates;
    private bool _isUpdating;
    private bool _updateAvailable;
    private string _platformLabel = "";
    private string _gamePath;

    public UpdaterViewModel()
    {
        _gamePath = ResolveGamePath();
        _platformLabel = GetPlatformLabel();
        LoadCurrentVersion();
        CleanOldFiles();

        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => CanCheckForUpdates);
        UpdateCommand = new AsyncRelayCommand(UpdateAsync, () => CanUpdate);
    }

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand UpdateCommand { get; }

    #region Properties

    public string CurrentVersion
    {
        get => _currentVersion;
        set => SetField(ref _currentVersion, value);
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set => SetField(ref _latestVersion, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string LogText
    {
        get => _logText;
        set => SetField(ref _logText, value);
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetField(ref _downloadProgress, value);
    }

    public double ExtractionProgress
    {
        get => _extractionProgress;
        set => SetField(ref _extractionProgress, value);
    }

    public string DownloadProgressText
    {
        get => _downloadProgressText;
        set => SetField(ref _downloadProgressText, value);
    }

    public string ExtractionProgressText
    {
        get => _extractionProgressText;
        set => SetField(ref _extractionProgressText, value);
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        set
        {
            SetField(ref _isCheckingForUpdates, value);
            OnPropertyChanged(nameof(CanCheckForUpdates));
            (CheckForUpdatesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set
        {
            SetField(ref _isUpdating, value);
            OnPropertyChanged(nameof(CanCheckForUpdates));
            OnPropertyChanged(nameof(CanUpdate));
            (CheckForUpdatesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (UpdateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set
        {
            SetField(ref _updateAvailable, value);
            OnPropertyChanged(nameof(CanUpdate));
            (UpdateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool CanCheckForUpdates => !_isCheckingForUpdates && !_isUpdating;

    public bool CanUpdate => _updateAvailable && !_isUpdating;

    public string PlatformLabel
    {
        get => _platformLabel;
        set => SetField(ref _platformLabel, value);
    }

    #endregion

    #region Public Methods

    public async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        StatusText = "Checking for updates...";
        AppendLog("Contacting GitHub for latest release info...");

        try
        {
            var response = await s_httpClient.GetAsync(GitHubApiUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            LatestVersion = tagName;

            var assets = root.GetProperty("assets");
            var expectedAssetName = GetExpectedAssetName();
            var found = false;

            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == expectedAssetName)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                StatusText = $"No release found for {_platformLabel}";
                AppendLog($"ERROR: Asset '{expectedAssetName}' not found in latest release.");
                return;
            }

            if (tagName != _currentVersion)
            {
                UpdateAvailable = true;
                StatusText = $"Update available: {tagName}";
                AppendLog($"New version found: {tagName} (current: {_currentVersion})");
            }
            else
            {
                UpdateAvailable = false;
                StatusText = "You are up to date!";
                AppendLog($"Already on the latest version: {tagName}");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Failed to check for updates";
            AppendLog($"ERROR: {ex.Message}");
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    public async Task UpdateAsync()
    {
        if (!CanUpdate) return;

        // Check if EndlessClient is running
        if (IsGameRunning())
        {
            StatusText = "EndlessClient is running — please close the game first";
            AppendLog("ERROR: Cannot update while EndlessClient is running. Please close the game and try again.");
            return;
        }

        IsUpdating = true;
        StatusText = "Starting update...";

        try
        {
            // Step 1: Download
            var archivePath = await DownloadReleaseAsync();
            if (archivePath == null)
            {
                StatusText = "Download failed";
                return;
            }

            // Step 2: Extract
            StatusText = "Extracting update...";
            await ExtractReleaseAsync(archivePath);

            // Step 3: Write version file
            var versionPath = Path.Combine(_gamePath, VersionFileName);
            await File.WriteAllTextAsync(versionPath, LatestVersion);

            CurrentVersion = LatestVersion;
            UpdateAvailable = false;

            StatusText = "Update complete!";
            AppendLog($"Successfully updated to {LatestVersion}");

            // Clean up downloaded archive
            try { File.Delete(archivePath); } catch { /* best effort */ }
        }
        catch (Exception ex)
        {
            StatusText = "Update failed";
            AppendLog($"ERROR: {ex.Message}");
        }
        finally
        {
            IsUpdating = false;
        }
    }

    #endregion

    #region Private Methods

    private string ResolveGamePath()
    {
        if (!string.IsNullOrEmpty(Program.GamePathOverride))
            return Path.GetFullPath(Program.GamePathOverride);

        // On macOS, if launched from within a .app bundle, resolve to the Resources directory
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var appDir = AppContext.BaseDirectory;
            // Check if we're inside a .app/Contents/Resources/ structure
            if (appDir.Contains(".app/Contents/Resources", StringComparison.OrdinalIgnoreCase))
            {
                return appDir.TrimEnd('/');
            }
        }

        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    private void LoadCurrentVersion()
    {
        var versionPath = Path.Combine(_gamePath, VersionFileName);
        if (File.Exists(versionPath))
        {
            CurrentVersion = File.ReadAllText(versionPath).Trim();
        }
        else
        {
            CurrentVersion = "Not installed / Unknown";
        }
    }

    private void CleanOldFiles()
    {
        try
        {
            var oldFiles = Directory.GetFiles(_gamePath, "*.old", SearchOption.AllDirectories);
            foreach (var oldFile in oldFiles)
            {
                try
                {
                    File.Delete(oldFile);
                }
                catch
                {
                    // best effort cleanup
                }
            }

            if (oldFiles.Length > 0)
            {
                AppendLog($"Cleaned up {oldFiles.Length} file(s) from previous update.");
            }
        }
        catch
        {
            // best effort
        }
    }

    private static bool IsGameRunning()
    {
        var processes = Process.GetProcessesByName("EndlessClient");
        return processes.Length > 0;
    }

    private static string GetPlatformLabel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows (x64)";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux (x64)";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS (Apple Silicon)";
        return "Unknown Platform";
    }

    private static string GetExpectedAssetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "EndlessClient-win-x64.zip";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "EndlessClient-linux-x64.tar.gz";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "EndlessClient-macos-arm64.zip";
        throw new PlatformNotSupportedException("Unsupported platform for auto-update");
    }

    private async Task<string?> DownloadReleaseAsync()
    {
        AppendLog("Fetching release info...");

        var response = await s_httpClient.GetAsync(GitHubApiUrl);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var expectedAssetName = GetExpectedAssetName();
        string? downloadUrl = null;
        long totalSize = 0;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == expectedAssetName)
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                totalSize = asset.GetProperty("size").GetInt64();
                break;
            }
        }

        if (downloadUrl == null)
        {
            AppendLog($"ERROR: Asset '{expectedAssetName}' not found.");
            return null;
        }

        var tempFile = Path.Combine(Path.GetTempPath(), expectedAssetName);
        AppendLog($"Downloading {expectedAssetName} ({FormatBytes(totalSize)})...");
        StatusText = "Downloading...";

        using var downloadResponse = await s_httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        downloadResponse.EnsureSuccessStatusCode();

        await using var contentStream = await downloadResponse.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;

            if (totalSize > 0)
            {
                DownloadProgress = (double)totalRead / totalSize * 100;
                DownloadProgressText = $"{FormatBytes(totalRead)} / {FormatBytes(totalSize)}";
            }
        }

        DownloadProgress = 100;
        DownloadProgressText = $"{FormatBytes(totalSize)} / {FormatBytes(totalSize)}";
        AppendLog("Download complete.");

        return tempFile;
    }

    private async Task ExtractReleaseAsync(string archivePath)
    {
        AppendLog($"Extracting to: {_gamePath}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractTarGzAsync(archivePath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await ExtractMacOsZipAsync(archivePath);
        }
        else
        {
            await ExtractZipAsync(archivePath);
        }

        AppendLog("Extraction complete.");
    }

    private async Task ExtractZipAsync(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var totalEntries = archive.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
        int processed = 0;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories

            var destPath = Path.Combine(_gamePath, entry.FullName);
            await ExtractEntryToPathAsync(entry, destPath);

            processed++;
            ExtractionProgress = (double)processed / totalEntries * 100;
            ExtractionProgressText = $"{processed} / {totalEntries} files";
        }
    }

    private async Task ExtractMacOsZipAsync(string archivePath)
    {
        // On macOS, the archive contains EndlessClient.app/, SettingsEditor.app/, ClientUpdater.app/
        // We need to extract individual files from EndlessClient.app/Contents/Resources/ into _gamePath
        // (which IS the Resources directory)
        using var archive = ZipFile.OpenRead(archivePath);

        const string resourcesPrefix = "EndlessClient.app/Contents/Resources/";

        var relevantEntries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && e.FullName.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalEntries = relevantEntries.Count;
        int processed = 0;

        foreach (var entry in relevantEntries)
        {
            var relativePath = entry.FullName[resourcesPrefix.Length..];
            if (string.IsNullOrEmpty(relativePath)) continue;

            var destPath = Path.Combine(_gamePath, relativePath);
            await ExtractEntryToPathAsync(entry, destPath);

            processed++;
            ExtractionProgress = (double)processed / totalEntries * 100;
            ExtractionProgressText = $"{processed} / {totalEntries} files";
        }

        // Also update launcher script and Info.plist if present
        await ExtractAppMetadataAsync(archive, "EndlessClient.app");
    }

    private async Task ExtractAppMetadataAsync(ZipArchive archive, string appName)
    {
        var macOsPrefix = $"{appName}/Contents/MacOS/";
        var plistPath = $"{appName}/Contents/Info.plist";

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith(macOsPrefix, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
            {
                // Resolve to the actual .app bundle parent
                // _gamePath is .../EndlessClient.app/Contents/Resources
                var appContents = Directory.GetParent(_gamePath)?.FullName;
                if (appContents == null) continue;

                var relativePath = entry.FullName[$"{appName}/Contents/".Length..];
                var destPath = Path.Combine(appContents, relativePath);
                await ExtractEntryToPathAsync(entry, destPath);
                AppendLog($"  Updated: {relativePath}");
            }
            else if (string.Equals(entry.FullName, plistPath, StringComparison.OrdinalIgnoreCase))
            {
                var appContents = Directory.GetParent(_gamePath)?.FullName;
                if (appContents == null) continue;

                var destPath = Path.Combine(appContents, "Info.plist");
                await ExtractEntryToPathAsync(entry, destPath);
                AppendLog("  Updated: Info.plist");
            }
        }
    }

    private async Task ExtractTarGzAsync(string archivePath)
    {
        const string stripPrefix = "linux-x64/";

        // First pass: count entries
        int totalEntries;
        await using (var countFs = File.OpenRead(archivePath))
        await using (var countGzip = new GZipStream(countFs, CompressionMode.Decompress))
        {
            using var countReader = new TarReader(countGzip);
            totalEntries = 0;
            while (await countReader.GetNextEntryAsync() is { } entry)
            {
                if (entry.EntryType == TarEntryType.RegularFile)
                    totalEntries++;
            }
        }

        // Second pass: extract
        await using var fs = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(fs, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        int processed = 0;

        while (await reader.GetNextEntryAsync() is { } entry)
        {
            if (entry.EntryType != TarEntryType.RegularFile)
                continue;

            var entryName = entry.Name;
            if (entryName.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
                entryName = entryName[stripPrefix.Length..];

            if (string.IsNullOrEmpty(entryName)) continue;

            var destPath = Path.Combine(_gamePath, entryName);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir != null && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            if (IsSettingsIni(entryName))
            {
                await MergeSettingsIniFromTarAsync(entry, destPath);
            }
            else
            {
                await SafeWriteFileAsync(destPath, async targetPath =>
                {
                    await using var destStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    if (entry.DataStream != null)
                        await entry.DataStream.CopyToAsync(destStream);
                });
            }

            processed++;
            ExtractionProgress = (double)processed / totalEntries * 100;
            ExtractionProgressText = $"{processed} / {totalEntries} files";
        }
    }

    private async Task ExtractEntryToPathAsync(ZipArchiveEntry entry, string destPath)
    {
        var destDir = Path.GetDirectoryName(destPath);
        if (destDir != null && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        var relativeName = entry.FullName;
        if (IsSettingsIni(relativeName))
        {
            await MergeSettingsIniFromZipAsync(entry, destPath);
            return;
        }

        await SafeWriteFileAsync(destPath, async targetPath =>
        {
            entry.ExtractToFile(targetPath, overwrite: true);
            await Task.CompletedTask;
        });
    }

    private async Task MergeSettingsIniFromZipAsync(ZipArchiveEntry entry, string destPath)
    {
        if (!File.Exists(destPath))
        {
            // No existing settings, just extract
            entry.ExtractToFile(destPath, overwrite: true);
            AppendLog("  config/settings.ini — new file (no merge needed)");
            return;
        }

        // Extract new settings to a temp file
        var tempNewSettings = Path.GetTempFileName();
        try
        {
            entry.ExtractToFile(tempNewSettings, overwrite: true);
            MergeSettingsIni(destPath, tempNewSettings);
        }
        finally
        {
            try { File.Delete(tempNewSettings); } catch { }
        }
    }

    private async Task MergeSettingsIniFromTarAsync(TarEntry entry, string destPath)
    {
        if (!File.Exists(destPath))
        {
            // No existing settings, just extract
            await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            if (entry.DataStream != null)
                await entry.DataStream.CopyToAsync(destStream);
            AppendLog("  config/settings.ini — new file (no merge needed)");
            return;
        }

        // Extract new settings to a temp file
        var tempNewSettings = Path.GetTempFileName();
        try
        {
            await using var tempStream = new FileStream(tempNewSettings, FileMode.Create, FileAccess.Write, FileShare.None);
            if (entry.DataStream != null)
                await entry.DataStream.CopyToAsync(tempStream);

            MergeSettingsIni(destPath, tempNewSettings);
        }
        finally
        {
            try { File.Delete(tempNewSettings); } catch { }
        }
    }

    private void MergeSettingsIni(string existingPath, string newPath)
    {
        // Load both INI files
        var existingIni = new IniReader(existingPath);
        var newIni = new IniReader(newPath);

        var existingLoaded = existingIni.Load();
        var newLoaded = newIni.Load();

        if (!newLoaded)
        {
            AppendLog("  config/settings.ini — could not parse new settings, skipping merge");
            return;
        }

        if (!existingLoaded)
        {
            // Can't read existing, just overwrite with new
            File.Copy(newPath, existingPath, overwrite: true);
            AppendLog("  config/settings.ini — replaced (could not parse existing)");
            return;
        }

        // Merge: start with new INI as base, overlay existing user values
        int newKeysAdded = 0;
        int userValuesPreserved = 0;

        foreach (var section in newIni.Sections)
        {
            if (existingIni.Sections.TryGetValue(section.Key, out var existingSection))
            {
                var overrides = new List<KeyValuePair<string, string>>();
                foreach (var kvp in section.Value)
                {
                    if (existingSection.TryGetValue(kvp.Key, out var userValue))
                    {
                        // User has this key — keep their value in the new INI
                        overrides.Add(new KeyValuePair<string, string>(kvp.Key, userValue));
                        userValuesPreserved++;
                    }
                    else
                    {
                        // New key added in update
                        newKeysAdded++;
                    }
                }

                foreach (var kvp in overrides)
                    section.Value[kvp.Key] = kvp.Value;
            }
            else
            {
                // Entirely new section from the update
                newKeysAdded += section.Value.Count;
            }
        }

        // Save merged result
        newIni.Save();
        File.Copy(newPath, existingPath, overwrite: true);

        AppendLog($"  config/settings.ini — merged ({newKeysAdded} new setting(s), {userValuesPreserved} user value(s) preserved)");
    }

    private async Task SafeWriteFileAsync(string destPath, Func<string, Task> writeAction)
    {
        try
        {
            await writeAction(destPath);
        }
        catch (IOException)
        {
            // File is likely locked (could be the updater itself)
            // Rename existing to .old and write new file
            var oldPath = destPath + ".old";
            try
            {
                if (File.Exists(oldPath))
                    File.Delete(oldPath);
                File.Move(destPath, oldPath);
                AppendLog($"  Renamed locked file: {Path.GetFileName(destPath)} → {Path.GetFileName(oldPath)}");
                await writeAction(destPath);
            }
            catch (Exception ex)
            {
                AppendLog($"  WARNING: Could not update {Path.GetFileName(destPath)}: {ex.Message}");
            }
        }
    }

    private static bool IsSettingsIni(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.EndsWith("config/settings.ini", StringComparison.OrdinalIgnoreCase);
    }

    private void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
