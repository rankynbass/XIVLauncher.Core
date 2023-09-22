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
    public static bool IsManagedWine => Program.Config.WineType == WineType.Managed;

    public static string CustomWinePath => Program.Config.WineBinaryPath ?? "/usr/bin";

    public static string FolderName => Program.Config.WineVersion ?? GetDefaultVersion();

    public static string DownloadUrl => GetDownloadUrl(Program.Config.WineVersion);

    public static string DebugVars => Program.Config.WineDebugVars ?? "-all";

    public static FileInfo LogFile => new FileInfo(Path.Combine(Program.storage.GetFolder("logs").FullName, "wine.log"));

    public static DirectoryInfo Prefix => Program.storage.GetFolder("wineprefix");

    public static bool ESyncEnabled => Program.Config.ESyncEnabled ?? true;

    public static bool FSyncEnabled => Program.Config.FSyncEnabled ?? false;

    // Proton additions
    public static bool IsProton => Program.Config.WineType == WineType.Proton;

    private static string RuntimePath => (IsProton && !OSInfo.IsFlatpak) ? Proton.GetRuntimePath(Program.Config.SteamRuntime) : "";

    private static string ProtonPath => IsProton ? Proton.GetVersionPath(Program.Config.ProtonVersion) : "";

    public static ProtonSettings? ProtonInfo
    {
        get {
            if (!IsProton) return null;
            return new ProtonSettings(Program.Config.SteamPath, ProtonPath, RuntimePath, Program.storage.GetFolder("protonprefix"),
                Program.Config.GamePath, Program.Config.GameConfigPath);
        }
    }

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static Wine()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize()
    {
        // Add default versions.
        Versions["wine-xiv-staging-fsync-git-7.10.r3.g560db77d"] = new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 7.10"}, {"desc","Patched version of Wine Staging 7.10. Default."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{OSInfo.Package.ToString()}-7.10.r3.g560db77d.tar.xz"},
            {"mark", "Download"}
        };
        Versions["wine-xiv-staging-fsync-git-8.5.r4.g4211bac7"] = new Dictionary<string, string>()
        {
            {"name", "Wine-XIV 8.5"}, {"desc", "Patched version of Wine Staging 8.5. Change Windows version to 7 for best results."},
            {"label", "Official"}, {"url", $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{OSInfo.Package.ToString()}-8.5.r4.g4211bac7.tar.xz"},
            {"mark", "Download"}
        };
        Versions["unofficial-wine-xiv-Proton8-15-x86_64"] = new Dictionary<string, string>()
        {
            {"name", "xiv-Proton8-15"}, {"desc", "Patched version of Wine-GE 8-15. Based on Proton8 Wine."},
            {"label", "Wine-GE"}, {"url", "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-15/unofficial-wine-xiv-Proton8-15-x86_64.tar.xz"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-Proton8-12-x86_64"] = new Dictionary<string, string>()
        {
            {"name", "xiv-Proton8-12"}, {"desc", "Patched version of Wine-GE 8-12. Based on Proton8 Wine."},
            {"label", "Wine-GE"}, {"url", "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-12/unofficial-wine-xiv-Proton8-12-x86_64.tar.xz"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-Proton7-43-x86_64"] = new Dictionary<string, string>()
        {
            {"name", "xiv-Proton7-43"}, {"desc", "Patched version of Wine-GE 7-43. Based on Proton7 Wine."},
            {"label", "Wine-GE"}, {"url", "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-43/unofficial-wine-xiv-Proton7-43-x86_64.tar.xz"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-git-8.16.0"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-git-8.16.0"}, {"desc", "Patched version of Wine Staging 8.16. Based on Wine-tkg."},
            {"label", "TKG"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.16.0/unofficial-wine-xiv-git-8.16.0.tar.xz"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-git-7.22.0"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-git-7.22.0"}, {"desc", "Patched version of Wine Staging 7.22. Based on Wine-tkg."},
            {"label", "TKG"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v7.22.0/unofficial-wine-xiv-git-7.22.0.tar.xz"},
            {"mark", "Download"}
        };



        var toolDirectory = new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine"));

        if (!toolDirectory.Exists)
        {
            Program.storage.GetFolder("compatibilitytool/wine");
            return;
        }

        foreach (var wineDir in toolDirectory.EnumerateDirectories())
        {
            if (File.Exists(Path.Combine(wineDir.FullName, "bin", "wine64")) ||
                File.Exists(Path.Combine(wineDir.FullName, "bin", "wine")))
            {
                if (Versions.ContainsKey(wineDir.Name))
                {
                    Versions[wineDir.Name].Remove("mark");
                    continue;
                }
                Versions[wineDir.Name] = new Dictionary<string, string>() { {"label", "Custom"} };
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
        if (Versions.ContainsKey("wine-xiv-staging-fsync-git-7.10.r3.g560db77d"))
            return "wine-xiv-staging-fsync-git-7.10.r3.g560db77d";
        if (Versions.ContainsKey("wine-xiv-staging-fsync-git-8.5.r4.g4211bac7"))
            return "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";
        return Versions.First().Key;
    }
}

public enum WineType
{
    [SettingsDescription("Managed by XIVLauncher", "Choose a patched version of wine made specifically for XIVLauncher")]
    Managed,

    [SettingsDescription("Proton", "Use Steam's Proton compatibility layer. Requires steam.")]
    Proton,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}