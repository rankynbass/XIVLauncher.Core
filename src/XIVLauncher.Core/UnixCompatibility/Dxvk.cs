using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.UnixCompatibility;

public static class Dxvk
{
    public static bool Enabled => Program.Config.DxvkVersion != "DISABLED";

    public static string FolderName => Program.Config.DxvkVersion ?? GetDefaultVersion();

    public static string DownloadUrl => GetDownloadUrl(FolderName);

    public static string NvapiFolderName => Program.Config.NvapiVersion ?? GetDefaultNvapiVersion();

    public static string NvapiDownloadUrl => GetNvapiDownloadUrl(NvapiFolderName);

    public static int FrameRateLimit => Program.Config.DxvkFrameRateLimit ?? 0;

    public static bool AsyncEnabled => Program.Config.DxvkAsyncEnabled ?? false;

    public static bool DxvkHudEnabled => Program.Config.DxvkHud != DxvkHud.None;

    public static string DxvkHudString => Program.Config.DxvkHud switch
    {
        DxvkHud.None => "",
        DxvkHud.Custom => Program.Config.DxvkHudCustom,
        DxvkHud.Default => "1",
        DxvkHud.Fps => "fps",
        DxvkHud.Full => "full",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static bool MangoHudInstalled { get; }

    public static bool MangoHudEnabled => Program.Config.MangoHud != MangoHud.None;

    public static bool MangoHudCustomIsFile => Program.Config.MangoHud == MangoHud.CustomFile;

    public static string MangoHudString => Program.Config.MangoHud switch
    {
        MangoHud.None => "",
        MangoHud.Default => "",
        MangoHud.Full => "full",
        MangoHud.CustomString => Program.Config.MangoHudCustomString,
        MangoHud.CustomFile => Program.Config.MangoHudCustomFile,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static string DXVK_HUD => "fps,frametimes,gpuload,version";

    public static string MANGOHUD_CONFIG => "ram,vram,resolution,vulkan_driver,engine_version,wine,frame_timing=0";

    public static string MANGOHUD_CONFIGFILE => Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".config", "MangoHud", "MangoHud.conf");

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    public static Dictionary<string, Dictionary<string, string>> NvapiVersions { get; private set; }

    static Dxvk()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
        NvapiVersions = new Dictionary<string, Dictionary<string, string>>();
        MangoHudInstalled = DxvkSettings.MangoHudIsInstalled();

        // Add default versions.
        Versions["dxvk-2.4"] = new Dictionary<string, string>()
        {
            {"name", "DXVK 2.4"}, {"desc", "Latest version, using Graphics Pipeline Libs. Async no longer needed."},
            {"label", "Current"}, {"url", "https://github.com/doitsujin/dxvk/releases/download/v2.4/dxvk-2.4.tar.gz"},
            {"mark", "Download" }
        };
        Versions["dxvk-2.2"] = new Dictionary<string, string>()
        {
            {"name", "DXVK 2.2"}, {"desc", "Previous version, using Graphics Pipeline Libs. Use this if you have problems with ReShade Effects Toggler (REST)."},
            {"label", "Previous"}, {"url", "https://github.com/doitsujin/dxvk/releases/download/v2.2/dxvk-2.2.tar.gz"},
            {"mark", "Download" }
        };
        Versions["dxvk-async-1.10.3"] = new Dictionary<string, string>()
        {
            {"name", "DXVK 1.10.3"}, {"desc", "Legacy version with high compatibility. Includes async patch."},
            {"label", "Legacy"}, {"url", "https://github.com/Sporif/dxvk-async/releases/download/1.10.3/dxvk-async-1.10.3.tar.gz"},
            {"mark", "Download" }
        };
        Versions["DISABLED"] = new Dictionary<string, string>()
        {
            {"name", "WineD3D"}, {"desc", "Use WineD3D (OpenGL) instead of DXVK. For old GPUs without Vulkan support."},
            {"label", "Disabled"}
        };

        NvapiVersions["dxvk-nvapi-v0.7.1"] = new Dictionary<string, string>()
        {
            {"name", "dxvk-nvapi 0.7.1"}, {"desc", "dxvk-nvapi 0.7.1. Latest version, should be compatible with latest Nvidia drivers." },
            {"label", "Current"}, {"url", "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.7.1/dxvk-nvapi-v0.7.1.tar.gz"},
            {"mark", "download"}
        };

        NvapiVersions["dxvk-nvapi-v0.6.4"] = new Dictionary<string, string>()
        {
            {"name", "dxvk-nvapi 0.6.4"}, {"desc", "dxvk-nvapi 0.6.4. Try this if 0.7.1 doesn't work." },
            {"label", "Current"}, {"url", "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.6.4/dxvk-nvapi-v0.6.4.tar.gz"},
            {"mark", "download"}
        };

        NvapiVersions["DISABLED"] = new Dictionary<string, string>()
        {
            {"name", "Disabled"}, {"desc", "Don't use Dxvk-nvapi. DLSS will not be available. (FSR2 mod still works)"},
            {"label", "DLSS Off"}
        };
    }

    public static void Initialize()
    {
        var toolDirectory = new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "dxvk"));

        if (!toolDirectory.Exists)
        {
            Program.storage.GetFolder("compatibilitytool/dxvk");
            return;
        }

        foreach (var dxvkDir in toolDirectory.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (Directory.Exists(Path.Combine(dxvkDir.FullName, "x64")) && Directory.Exists(Path.Combine(dxvkDir.FullName, "x32")))
            {
                if (dxvkDir.Name.Contains("nvapi"))
                {
                    if (NvapiVersions.ContainsKey(dxvkDir.Name))
                    {
                        NvapiVersions[dxvkDir.Name].Remove("mark");
                        continue;
                    }
                    NvapiVersions[dxvkDir.Name] = new Dictionary<string, string>() { {"label", "Custom"} };
                }
                else
                {
                    if (Versions.ContainsKey(dxvkDir.Name))
                    {
                        if (dxvkDir.Name == "DISABLED")
                            Log.Error("Cannot use custom DXVK with folder name DISABLED. Skipping.");
                        else
                            Versions[dxvkDir.Name].Remove("mark");
                        continue;
                    }
                    Versions[dxvkDir.Name] = new Dictionary<string, string>() { {"label", "Custom"} };
                }
            }
        }
    }

    private static string GetDownloadUrl(string? name)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
        return Versions[GetDefaultVersion()]["url"];
    }

    public static string GetDefaultVersion()
    {
        if (Versions.ContainsKey("dxvk-async-1.10.3"))
            return "dxvk-async-1.10.3";
        if (Versions.ContainsKey("dxvk-2.4"))
            return "dxvk-2.4";
        return Versions.First().Key;
    }

    public static string GetNvapiDownloadUrl(string? name)
    {
        name ??= GetDefaultNvapiVersion();
        if (NvapiVersions.ContainsKey(name))
            return NvapiVersions[name].ContainsKey("url") ? NvapiVersions[name]["url"] : "";
        return Versions[GetDefaultNvapiVersion()].ContainsKey("url") ? Versions[GetDefaultNvapiVersion()]["url"] : "";
    }

    public static string GetDefaultNvapiVersion()
    {
        if (NvapiVersions.ContainsKey("dxvk-nvapi-v0.7.1"))
            return "dxvk-nvapi-v0.7.1";
        return NvapiVersions.First().Key;
    }

}

public enum DxvkHud
{
    [SettingsDescription("None", "Disable DXVK Hud")]
    None,

    [SettingsDescription("FPS", "Only show FPS")]
    Fps,

    [SettingsDescription("Default", "Equivalent to DXVK_HUD=1")]
    Default,

    [SettingsDescription("Custom", "Use a custom DXVK_HUD string")]
    Custom,

    [SettingsDescription("Full", "Show everything")]
    Full,
}

public enum MangoHud
{
    [SettingsDescription("None", "Disable MangoHud")]
    None,

    [SettingsDescription("Default", "Uses no config file.")]
    Default,

    [SettingsDescription("Custom File", "Specify a custom config file")]
    CustomFile,

    [SettingsDescription("Custom String", "Specify a config via string")]
    CustomString,

    [SettingsDescription("Full", "Show (almost) everything")]
    Full,
}

