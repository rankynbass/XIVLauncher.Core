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
using XIVLauncher.Core;

namespace XIVLauncher.Core.UnixCompatibility;

public static class Dxvk
{
    public static bool MangoHudInstalled { get; }

    public static string DXVK_HUD => "fps,frametimes,gpuload,version";

    public static string MANGOHUD_CONFIG => "ram,vram,resolution,vulkan_driver,engine_version,wine,frame_timing=0";

    public static string MANGOHUD_CONFIGFILE => Path.Combine(CoreEnvironmentSettings.XDG_CONFIG_HOME, "MangoHud", "MangoHud.conf");

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    public static Dictionary<string, Dictionary<string, string>> NvapiVersions { get; private set; }

    static Dxvk()
    {
        MangoHudInstalled = DxvkSettings.MangoHudIsInstalled();
        var dlssStatus = CoreEnvironmentSettings.ForceDLSS ? "forced on with XL_FORCE_DLSS=1" : (CoreEnvironmentSettings.IsDLSSAvailable ? $"nvngx.dll found at {CoreEnvironmentSettings.NvidiaWineDLLPath()}" : "nvngx.dll not found");
        Log.Information($"DLSS: {dlssStatus}");
        Versions = new Dictionary<string, Dictionary<string, string>>();
        NvapiVersions = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize()
    {
        // Add default versions.
        Versions.Add("DISABLED", new Dictionary<string, string>()
        {
            {"name", "WineD3D"}, {"desc", "Use WineD3D (OpenGL) instead of DXVK. For old GPUs without Vulkan support."},
            {"label", "Disabled"}
        });
        Versions.Add("dxvk-2.4.1", new Dictionary<string, string>()
        {
            {"name", "2.4.1"}, {"desc", "Official version 2.4 of DXVK."},
            {"label", "Current"}, {"url", "https://github.com/doitsujin/dxvk/releases/download/v2.4.1/dxvk-2.4.1.tar.gz"},
            {"mark", "Download"}
        });
        Versions.Add("dxvk-gplasync-v2.4.1-1", new Dictionary<string, string>()
        {
            {"name", "2.4.1-1 GPLAsync"}, {"desc", "Latest version, using Graphics Pipeline Libs. GPL Async included."},
            {"label", "GPLAsync"}, {"url", "https://gitlab.com/Ph42oN/dxvk-gplasync/-/raw/main/releases/dxvk-gplasync-v2.4.1-1.tar.gz"},
            {"mark", "Download"}
        });
        Versions.Add("dxvk-2.2", new Dictionary<string, string>()
        {
            {"name", "2.2"}, {"desc", "Previous version, using Graphics Pipeline Libs. Use this if you have problems with ReShade Effects Toggler (REST)."},
            {"label", "Previous"}, {"url", "https://github.com/doitsujin/dxvk/releases/download/v2.2/dxvk-2.2.tar.gz"},
            {"mark", "Download" }
        });
        Versions.Add("dxvk-async-1.10.3", new Dictionary<string, string>()
        {
            {"name", "1.10.3"}, {"desc", "Legacy version with high compatibility. Includes async patch."},
            {"label", "Legacy"}, {"url", "https://github.com/Sporif/dxvk-async/releases/download/1.10.3/dxvk-async-1.10.3.tar.gz"},
            {"mark", "Download" }
        });

        if (CoreEnvironmentSettings.IsDLSSAvailable)
        {
            // Default dxvi-nvapi versions. Only add if DLSS is available.
            NvapiVersions.Add("dxvk-nvapi-v0.7.1", new Dictionary<string, string>()
            {
                {"name", "0.7.1"}, {"desc", "dxvk-nvapi 0.7.1. Latest version, should be compatible with latest Nvidia drivers." },
                {"label", "Current"}, {"url", "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.7.1/dxvk-nvapi-v0.7.1.tar.gz"},
                {"mark", "download"}
            });
            NvapiVersions.Add("dxvk-nvapi-v0.6.4", new Dictionary<string, string>()
            {
                {"name", "0.6.4"}, {"desc", "dxvk-nvapi 0.6.4. Try this if 0.7.1 doesn't work." },
                {"label", "Current"}, {"url", "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.6.4/dxvk-nvapi-v0.6.4.tar.gz"},
                {"mark", "download"}
            });
        }
        NvapiVersions.Add("DISABLED", new Dictionary<string, string>()
        {
            {"name", "Disabled"}, {"desc", "Disable native DLSS. Use this for the FSR2/3/XeSS mod."},
            {"label", "DLSS Off"}
        });

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
                    // Don't add anything to Nvapi if DLSS is not available.
                    if (!CoreEnvironmentSettings.IsDLSSAvailable)
                        continue;
                    if (NvapiVersions.ContainsKey(dxvkDir.Name))
                    {
                        NvapiVersions[dxvkDir.Name].Remove("mark");
                        continue;
                    }
                    NvapiVersions.Add(dxvkDir.Name, new Dictionary<string, string>() { {"label", "Custom"} });
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
                    Versions.Add(dxvkDir.Name, new Dictionary<string, string>() { {"label", "Custom"} });
                }
            }
        }
    }

    public static string GetDownloadUrl(string? name)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
        return Versions[GetDefaultVersion()]["url"];
    }

    public static string GetDefaultVersion()
    {
        if (Versions.ContainsKey("dxvk-2.4"))
            return "dxvk-2.4";
        if (Versions.ContainsKey("dxvk-async-1.10.3"))
            return "dxvk-async-1.10.3";
        return Versions.First().Key;
    }

    public static void SetDxvkMark(string name, string? mark)
    {
        if (Versions.ContainsKey(name))
        {
            if (!string.IsNullOrEmpty(mark))
                Versions[name]["mark"] = mark;
            else
                Versions[name].Remove("mark");
        }
    }

    public static void SetNvapiMark(string name, string? mark)
    {
        if (NvapiVersions.ContainsKey(name))
        {
            if (!string.IsNullOrEmpty(mark))
                NvapiVersions[name]["mark"] = mark;
            else
                NvapiVersions[name].Remove("mark");
        }
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

    public static void ReInitialize()
    {
        foreach (var dxvk in Versions)
            Versions.Remove(dxvk.Key);
        foreach (var nvapi in NvapiVersions)
            NvapiVersions.Remove(nvapi.Key);
        Initialize();
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

