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

public static class Vkd3d
{
    public static bool Enabled => Program.Config.Vkd3dVersion != "DISABLED";

    public static string FolderName => Program.Config.Vkd3dVersion ?? GetDefaultVersion();

    public static string DownloadUrl => GetDownloadUrl(Program.Config.Vkd3dVersion);

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static Vkd3d()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize()
    {
        // Add default versions.
        Versions["vkd3d-proton-2.10"] = new Dictionary<string, string>()
        {
            {"name", "VKD3D 2.10"}, {"desc", "Latest version of VKD3D. Requires DXVK 2.1 or higher"},
            {"label", "Current"}, {"url", "https://github.com/HansKristian-Work/vkd3d-proton/releases/download/v2.10/vkd3d-proton-2.10.tar.zst"},
            {"mark", "Download" }
        };
        Versions["vkd3d-proton-2.8"] = new Dictionary<string, string>()
        {
            {"name", "VKD3D 2.8"}, {"desc", "Requires Wine/Proton-wine 8+, but should work with DXVK 1.10.3."},
            {"label", "Legacy"}, {"url", "https://github.com/HansKristian-Work/vkd3d-proton/releases/download/v2.8/vkd3d-proton-2.8.tar.zst"},
            {"mark", "Download" }
        };
        Versions["vkd3d-proton-2.6"] = new Dictionary<string, string>()
        {
            {"name", "VKD3D 2.6"}, {"desc", "Works with Wine/Proton-wine 7 and DXVK 1.10.3."},
            {"label", "Legacy"}, {"url", "https://github.com/HansKristian-Work/vkd3d-proton/releases/download/v2.6/vkd3d-proton-2.6.tar.zst"},
            {"mark", "Download" }
        };
        Versions["DISABLED"] = new Dictionary<string, string>()
        {
            {"name", "Disabled"}, {"desc", "Don't use VKD3D. Only use DXVK or WineD3D"},
            {"label", "Disabled"}
        };

        var toolDirectory = new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "vkd3d"));

        if (!toolDirectory.Exists)
        {
            Program.storage.GetFolder("compatibilitytool/vkd3d");
            return;
        }

        foreach (var vkd3dDir in toolDirectory.EnumerateDirectories())
        {
            if (Directory.Exists(Path.Combine(vkd3dDir.FullName, "x64")))
            {
                if (Versions.ContainsKey(vkd3dDir.Name))
                {
                    if (vkd3dDir.Name == "DISABLED")
                        Log.Error("Cannot use custom VKD3D with folder name DISABLED. Skipping.");
                    else
                        Versions[vkd3dDir.Name].Remove("mark");
                    continue;
                }
                Versions[vkd3dDir.Name] = new Dictionary<string, string>() { {"label", "Custom"} };
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
        if (Versions.ContainsKey("vkd3d-proton-2.6"))
            return "vkd3d-proton-2.6";
        if (Versions.ContainsKey("vkd3d-proton-2.10"))
            return "vkd3d-proton-2.10";
        return Versions.First().Key;
    }
}

public enum DXR
{
    [SettingsDescription("DXR Enabled", "Enable DXR 1.0 Ray Tracing")]
    Enabled,

    [SettingsDescription("DXR 1.1 Enabled", "Enable DXR 1.1 Ray Tracing")]
    Enabled11,

    [SettingsDescription("Disabled", "Turn off Ray Tracing")]
    Disabled,
}
