using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class Dxvk
{
    private static string DownloadUrl = "https://github.com/Sporif/dxvk-async/releases/download/1.10.1/dxvk-async-1.10.1.tar.gz";
    private static string FileName = "dxvk-async-1.10.1";
    private static string Release = "1.10.1";
    public static DxvkVersion Version { get; set; } = Dxvk.DxvkVersion.v1_10_1;

    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory)
    {
        Release = Version switch
        {
            Dxvk.DxvkVersion.v1_10_1 => "1.10.1",
            Dxvk.DxvkVersion.v1_10_2 => "1.10.2",
            Dxvk.DxvkVersion.v1_10_3 => "1.10.3",
            Dxvk.DxvkVersion.v2_0 => "2.0",
        };
        FileName = $"dxvk-async-{Release}";
        DownloadUrl = $"https://github.com/Sporif/dxvk-async/releases/download/{Release}/{FileName}.tar.gz";

        var dxvkPath = Path.Combine(installDirectory.FullName, FileName, "x64");

        if (!Directory.Exists(dxvkPath))
        {
            Log.Information("DXVK does not exist, downloading");
            await DownloadDxvk(installDirectory).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }
    }

    private static async Task DownloadDxvk(DirectoryInfo installDirectory)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(DownloadUrl));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    public enum DxvkHudType
    {
        [SettingsDescription("None", "Show nothing")]
        None,

        [SettingsDescription("FPS", "Only show FPS")]
        Fps,

        [SettingsDescription("Full", "Show everything")]
        Full,
    }

    public enum DxvkVersion
    {
        [SettingsDescription("1.10.1 (default)", "The default version of DXVK used with XIVLauncher.Core.")]
        v1_10_1,

        [SettingsDescription("1.10.2", "Newer version of 1.10 branch of DXVK. Probably works.")]
        v1_10_2,

       [SettingsDescription("1.10.3", "Newest version of 1.10 branch of DXVK. Probably works.")]
        v1_10_3,

       [SettingsDescription("2.0 (unsafe)", "New 2.0 version of DXVK. Might break Dalamud or GShade.")]
        v2_0,
    }
}