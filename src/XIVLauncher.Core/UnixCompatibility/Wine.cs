using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.UnixCompatibility;

public static class Wine
{
    public const string DEFAULT = "wine-xiv-staging-fsync-git-10.8.r0.g47f77594";

    internal const string FALLBACK = "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";

    private static string distro => (System.Environment.GetEnvironmentVariable("XL_DISTRO") ?? "").ToLower() switch
    {
        "arch" => "arch",
        "fedora" => "fedora",
        "ubuntu" => "ubuntu",
        _ => LinuxInfo.Package.ToString(),
    };

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static Wine()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize()
    {
        // Add default versions.
        Versions.Add("unofficial-wine-xiv-staging-10.10", new Dictionary<string, string>()
        {
            {"name", "Unofficial Wine-XIV 10.10"}, {"desc", "Patched version of Wine Staging 10.10. Now with wayland and lsteamclient support added."},
            {"label", "XIV-Staging"}, {"url", $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-{distro}-10.10.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-staging-ntsync-10.10", new Dictionary<string, string>()
        {
            {"name", "Unofficial Wine-XIV 10.10 NTSync"}, {"desc", "Patched version of Wine Staging 10.10. NTSync version. Requires compatible kernel."},
            {"label", "XIV-NTSync"}, {"url", $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-ntsync-10.10.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-staging-8.21", new Dictionary<string, string>()
        {
            {"name", "Unofficial Wine-XIV 8.21"}, {"desc", "Patched version of Wine Staging 8.21. Now with wayland and lsteamclient support added."},
            {"label", "XIV-Staging"}, {"url", $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.21-20250101/unofficial-wine-xiv-staging-{distro}-8.21.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-valvebe-9-20", new Dictionary<string, string>()
        {
            {"name", "Unofficial ValveBE 9-20"}, {"desc", "Patched Valve Wine 9. A replacement for wine-ge, since it's discontinued. Includes staging patches."},
            {"label", "XIV-ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-20/unofficial-wine-xiv-valvebe-9-20.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("unofficial-wine-xiv-Proton8-26-x86_64", new Dictionary<string, string>()
        {
            {"name", "Wine-GE-XIV 8-26"}, {"desc", "Patched version of Wine-GE 8-26. Based on Proton8 Wine."},
            {"label", "Wine-GE-XIV"}, {"url", "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-26/unofficial-wine-xiv-Proton8-26-x86_64.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("wine-xiv-staging-fsync-git-10.8.r0.g47f77594", new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 10.8"}, {"desc","Patched version of Wine Staging 10.8. Default."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/10.8.r0.g47f77594/wine-xiv-staging-fsync-git-{distro}-10.8.r0.g47f77594.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("wine-xiv-staging-fsync-git-8.5.r4.g4211bac7", new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 8.5"}, {"desc", "Patched version of Wine Staging 8.5. Change Windows version to 7 for best results."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{distro}-8.5.r4.g4211bac7.tar.xz"},
            {"mark", "Download"}
        });

        Versions.Add("wine-xiv-staging-fsync-git-7.10.r3.g560db77d", new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 7.10"}, {"desc","Patched version of Wine Staging 7.10."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{distro}-7.10.r3.g560db77d.tar.xz"},
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
                string label;
                string dirname = wineDir.Name.ToLower();
                if (dirname.Contains("ge-proton"))
                    label = "Wine-GE";
                else if (dirname.StartsWith("wine-xiv-staging"))
                    label = "Official";
                else if (dirname.StartsWith("unofficial-wine-xiv"))
                {
                    if (dirname.Contains("proton"))
                        label = "Wine-GE-XIV";
                    else if (dirname.Contains("valvebe"))
                        label = "XIV-ValveBE";
                    else if (dirname.Contains("ntsync"))
                        label = "XIV-NTSync";
                    else if (dirname.Contains("staging"))
                        label = "XIV-Staging";
                    else
                        label = "XIV-patched";
                }
                else
                    label = "Custom";
                Versions.Add(wineDir.Name, new Dictionary<string, string>() { {"label", label} });
            }
        }
    }

    internal static string GetVersion(string? name, bool fullName = true)
    {
        name ??= GetDefaultVersion();
        var path = Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine");
        if (Versions.ContainsKey(name))
            return fullName ? Path.Combine(path, name) : name;
        return fullName ? Path.Combine(path, GetDefaultVersion()) : GetDefaultVersion();
    }

    internal static string GetDownloadUrl(string? name)
    {
        if (Program.Config.RunnerType == RunnerType.Custom)
            return "";
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
        return Versions[GetDefaultVersion()].ContainsKey("url") ? Versions[GetDefaultVersion()]["url"] : "";
    }

    public static string GetDefaultVersion()
    {
        if (Versions.ContainsKey(DEFAULT))
            return DEFAULT;
        // Just in case DEFAULT doesn't get updated properly
        if (Versions.ContainsKey(FALLBACK))
            return FALLBACK;
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