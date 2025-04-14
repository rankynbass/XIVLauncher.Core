using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Serilog;

using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.Dxvk;

public enum DxvkHudType
{
    [SettingsDescription("None", "Show nothing")]
    None,

    [SettingsDescription("FPS", "Only show FPS")]
    Fps,

    [SettingsDescription("Full", "Show everything")]
    Full,
}

public static class Dxvk
{
    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory, string version)
    {
        if (version is "Disabled")
        {
            return;
        }

        CompatToolRelease release = CompatToolbox.GetTool("Dxvk", version);

        var dxvkPath = Path.Combine(installDirectory.FullName, release.Folder, "x64");
        if (!Directory.Exists(dxvkPath))
        {
            var installPath = new DirectoryInfo(release.TopLevelFolder ? installDirectory.FullName : Path.Combine(installDirectory.FullName, release.Folder));
            if (!installPath.Exists)
                installPath.Create();
            Log.Information("DXVK does not exist, downloading");
            await DownloadDxvk(installPath, release.DownloadUrl).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (var fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }
    }

    private static async Task DownloadDxvk(DirectoryInfo installDirectory, string url)
    {
        using var client = new HttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(url).ConfigureAwait(false));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }
}

