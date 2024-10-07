using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.UnixCompatibility;

public static class Wine
{
    public const string DEFAULT = "unofficial-wine-xiv-staging-9.16";

    public static string Folder => GetVersion(Program.Config.WineVersion);

    public static string DownloadUrl => GetDownloadUrl(Program.Config.WineVersion);

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static Wine()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize()
    {
        // Add default versions.
        Versions.Add("unofficial-wine-xiv-staging-9.16", new Dictionary<string, string>()
        {
            {"name", "Unofficial Wine-XIV 9.16"}, {"desc", "Patched version of Wine Staging 9.13. Now with wayland and lsteamclient support added."},
            {"label", "Staging"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v9.16/unofficial-wine-xiv-staging-9.16.tar.zst"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-staging-9.13.1", new Dictionary<string, string>()
        {
            {"name", "Unofficial Wine-XIV 9.13.1"}, {"desc", "Patched version of Wine Staging 9.13. Now with wayland and lsteamclient support added."},
            {"label", "Staging"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v9.13.1/unofficial-wine-xiv-staging-9.13.1.tar.zst"},
            {"mark", "Download"}
        });

        Versions.Add("wine-xiv-staging-fsync-git-9.17.r0.g27b121f2", new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 9.17"}, {"desc", "Patched version of Wine Staging 9.17. Change Windows version to 10 for best results."},
            {"label", "Testing"}, {"url", $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/beta-9.17.r0.g27b121f2/wine-xiv-staging-fsync-git-{OSInfo.Package.ToString()}-9.17.r0.g27b121f2.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("wine-xiv-staging-fsync-git-8.5.r4.g4211bac7", new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 8.5"}, {"desc", "Patched version of Wine Staging 8.5. Change Windows version to 7 for best results."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{OSInfo.Package.ToString()}-8.5.r4.g4211bac7.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("wine-xiv-staging-fsync-git-7.10.r3.g560db77d", new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 7.10"}, {"desc","Patched version of Wine Staging 7.10. Default."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{OSInfo.Package.ToString()}-7.10.r3.g560db77d.tar.xz"},
            {"mark", "Download"}
        });
        
        Versions.Add("unofficial-wine-xiv-Proton8-26-x86_64", new Dictionary<string, string>()
        {
            {"name", "Wine-GE-XIV 8-26"}, {"desc", "Patched version of Wine-GE 8-26. Based on Proton8 Wine."},
            {"label", "Wine-GE"}, {"url", "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-26/unofficial-wine-xiv-Proton8-26-x86_64.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-valvebe-8-2", new Dictionary<string, string>()
        {
            {"name", "Unofficial ValveBE 8-2"}, {"desc", "Patched Valve Wine 8. A replacement for wine-ge, since it's discontinued."},
            {"label", "ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-8-2/unofficial-wine-xiv-valvebe-8-2.tar.zst"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-valvebe-9-09", new Dictionary<string, string>()
        {
            {"name", "Unofficial ValveBE 9-09"}, {"desc", "Patched Valve Wine 9. A replacement for wine-ge, since it's discontinued. Includes staging patches."},
            {"label", "ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-09/unofficial-wine-xiv-valvebe-9-09.tar.zst"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-valvebe-9-10", new Dictionary<string, string>()
        {
            {"name", "Unofficial ValveBE 9-10"}, {"desc", "Patched Valve Wine 9. A replacement for wine-ge, since it's discontinued. Includes staging patches."},
            {"label", "ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-10/unofficial-wine-xiv-valvebe-9-10.tar.zst"},
            {"mark", "Download"}
        });

        var toolDirectory = new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine"));

        if (!toolDirectory.Exists)
        {
            Program.storage.GetFolder("compatibilitytool/wine");
            return;
        }

        foreach (var wineDir in toolDirectory.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (File.Exists(Path.Combine(wineDir.FullName, "bin", "wine64")) ||
                File.Exists(Path.Combine(wineDir.FullName, "bin", "wine")))
            {
                if (Versions.ContainsKey(wineDir.Name))
                {
                    Versions[wineDir.Name].Remove("mark");
                    continue;
                }
                Versions.Add(wineDir.Name, new Dictionary<string, string>() { {"label", "Custom"} });
            }
        }
    }

    public static string GetVersion(string? name, bool folderOnly = false)
    {
        if (Program.Config.RunnerType == RunnerType.Custom)
            return Program.Config.WineBinaryPath ?? "/usr/bin";
        name ??= GetDefaultVersion();
        var path = Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine");
        if (Versions.ContainsKey(name))
            return folderOnly ? name : Path.Combine(path, name);
        return folderOnly ? GetDefaultVersion() : Path.Combine(path, GetDefaultVersion());
    }

    public static string GetDownloadUrl(string? name)
    {
        if (Program.Config.RunnerType == RunnerType.Custom)
            return "";
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
        return Versions[GetDefaultVersion()]["url"];
    }

    public static string GetDefaultVersion()
    {
        if (Versions.ContainsKey(DEFAULT))
            return DEFAULT;
        return Versions.First().Key;
    }

    public static void SetMark(string name, string? mark)
    {
        if (Versions.ContainsKey(name))
        {
            if (!string.IsNullOrEmpty(mark))
                Versions[name]["mark"] = mark;
            else
                Versions[name].Remove("mark");
        }
    }

    public static void ReInitialize()
    {
        foreach (var wine in Versions)
            Versions.Remove(wine.Key);
        Initialize();
    }
}