using System.Numerics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using Serilog;
using XIVLauncher.Core;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.UnixCompatibility;

public static class SteamCompatibilityTool
{
    private const string XLM_URL = "https://github.com/Blooym/xlm/releases/latest/download/xlm-x86_64-unknown-linux-gnu";
    public static bool IsSteamInstalled => Directory.Exists(Program.Config.SteamPath);

    public static bool IsSteamFlatpakInstalled => Directory.Exists(Program.Config.SteamFlatpakPath);

    public static bool IsSteamSnapInstalled => Directory.Exists(Program.Config.SteamSnapPath);

    public static bool IsSteamToolInstalled => Directory.Exists(Path.Combine(Program.Config.SteamPath, "compatibilitytools.d", "XLM"));

    public static bool IsSteamFlatpakToolInstalled => Directory.Exists(Path.Combine(Program.Config.SteamFlatpakPath, "compatibilitytools.d", "XLM"));

    public static bool IsSteamSnapToolInstalled => Directory.Exists(Path.Combine(Program.Config.SteamSnapPath, "compatibilitytools.d", "XLM"));

    public static void DeleteOldTools()
    {
        var paths = new string[] { Program.Config.SteamPath, Program.Config.SteamFlatpakPath, Program.Config.SteamSnapPath };
        foreach (var path in paths)
        {
            var steamToolFolder = new DirectoryInfo(Path.Combine(path, "compatibilitytools.d", "xlcore"));
            if (!steamToolFolder.Exists) return;
            steamToolFolder.Delete(true);
            Log.Verbose($"[SCT] Deleted Steam compatibility tool at folder {steamToolFolder.FullName}");
        }
    }

    private static async Task<bool> DownloadTool(string tempDirectory, string tempName, string downloadUrl, bool untar = false)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();
        var downloadedFile = new FileInfo(tempPath);

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
        try
        {
            if (!downloadedFile.Exists)
                return false;
            if (downloadedFile.Length == 0)
                return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not download file from {downloadUrl} or could not write file to disk.");
            Console.WriteLine(ex);
            return false;
        }

        if (untar)
            PlatformHelpers.Untar(tempPath, Path.Combine(tempDirectory, tempName));
        else
            File.Move(tempPath, Path.Combine(tempDirectory, tempName), true);

        File.Delete(tempPath);
        return true;
    }

    public static async Task<bool> InstallXLM(string? steamPath = null)
    {
        steamPath ??= Path.Combine(CoreEnvironmentSettings.XDG_DATA_HOME, "Steam");
        var compatPath = Path.Combine(steamPath, "compatibilitytools.d");
        if (!Directory.Exists(steamPath))
        {
            Log.Error($"Folder {steamPath} does not exist! Cannot install xlm to this location.");
            return false;
        }
        var secretEnv = (Program.IsSteamDeckHardware || steamPath == Program.Config.SteamFlatpakPath) ? "--use-fallback-secret-provider " : "";
        var tempPath = Path.Combine(Program.storage.Root.FullName, "temp");
        var permissions =   UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        
        var downloaded = await DownloadTool(tempPath, "xlm", XLM_URL, false).ConfigureAwait(false);

        if (!downloaded)
        {
            Log.Error($"Could not download XLM from {XLM_URL}");
            return false;
        }

        File.SetUnixFileMode(Path.Combine(tempPath, "xlm"), permissions);

        var xlm = new Process();
        xlm.StartInfo.FileName = Path.Combine(tempPath, "xlm");
        xlm.StartInfo.Arguments = $"install-steam-tool --steam-compat-path {compatPath} --extra-launch-args=\"{secretEnv}--xlcore-repo-owner rankynbass --xlcore-repo-name XIVLauncher.Core\"";
        xlm.Start();

        await xlm.WaitForExitAsync();
        File.Delete(Path.Combine(tempPath, "xlm"));
        Log.Verbose($"Installed XLM to {compatPath}");
        return true;
    }

    public static void UninstallXLM(string? steamPath = null)
    {
        steamPath ??= Path.Combine(CoreEnvironmentSettings.XDG_DATA_HOME, "Steam");
        var xlmFolder = new DirectoryInfo(Path.Combine(steamPath, "compatibilitytools.d", "XLM"));
        if (!xlmFolder.Exists) return;
        xlmFolder.Delete(true);
        Log.Verbose($"[SCT] Deleted Steam compatibility tool at folder {xlmFolder.FullName}");
    }

}