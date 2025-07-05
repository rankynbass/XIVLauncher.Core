using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

using Serilog;

using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.Dxvk;

public enum RBHudType
{
    [SettingsDescription("None", "Show nothing")]
    None,

    [SettingsDescription("Dxvk Hud FPS", "Only show FPS")]
    Fps,

    [SettingsDescription("Dxvk Hud Full", "Show everything")]
    Full,

    [SettingsDescription("Dxvk Hud Custom String", "Specify a custom DXVK Hud string")]
    Custom,

    [SettingsDescription("MangoHud Default", "Default display. Uses no config file or string.")]
    MHDefault,

    [SettingsDescription("MangoHud Custom File", "Specify a custom config file")]
    MHCustomFile,

    [SettingsDescription("MangoHud Custom String", "Specify a config via string")]
    MHCustomString,

    [SettingsDescription("MangoHud Full", "Show (almost) everything")]
    MHFull,
}

public static class Dxvk
{
    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory, IToolRelease release)
    {
        if (release.Name == "DISABLED")
        {
            return;
        }

        var dxvkPath = Path.Combine(installDirectory.FullName, release.Name, "x64");
        if (!Directory.Exists(dxvkPath))
        {
            Log.Information("DXVK does not exist, downloading");
            await DownloadDxvk(installDirectory, release.DownloadUrl, release.Checksum).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (var fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }
    }

    private static async Task DownloadDxvk(DirectoryInfo installDirectory, string url, string checksum)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentOutOfRangeException("Download URL is null or empty");
        
        using var client = HappyEyeballsHttp.CreateHttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(url).ConfigureAwait(false));

        if (!CompatUtil.EnsureChecksumMatch(tempPath, [checksum]))
        {
            throw new InvalidDataException("SHA512 checksum verification failed");
        }

        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    public const string MANGOHUD_DEFAULT_STRING = "ram,vram,resolution,vulkan_driver,engine_version,wine,frame_timing=1";

    public static bool IsDxvkHudStringValid(string customHud)
    {
        var ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";
        var ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (customHud == "full") return true;
        if (customHud == "1") return true;
        if (!Regex.IsMatch(customHud, ALLOWED_CHARS)) return false;

        string[] hudvars = customHud.Split(",");

        return hudvars.All(hudvar => Regex.IsMatch(hudvar, ALLOWED_WORDS));        
    }
}

