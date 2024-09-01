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
        
        Versions["unofficial-wine-xiv-Proton8-26-x86_64"] = new Dictionary<string, string>()
        {
            {"name", "xiv-Proton8-26"}, {"desc", "Patched version of Wine-GE 8-26. Based on Proton8 Wine."},
            {"label", "Wine-GE"}, {"url", "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-26/unofficial-wine-xiv-Proton8-26-x86_64.tar.xz"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-valvebe-8-2"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-valvebe-8-2"}, {"desc", "Patched Valve Wine 8. A replacement for wine-ge, since it's discontinued."},
            {"label", "ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-8-2/unofficial-wine-xiv-valvebe-8-2.tar.zst"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-valvebe-9-07"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-valvebe-9-07"}, {"desc", "Patched Valve Wine 9. A replacement for wine-ge, since it's discontinued. Includes staging patches."},
            {"label", "ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-07/unofficial-wine-xiv-valvebe-9-07.tar.zst"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-valvebe-9-07-clean"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-valvebe-9-07-clean"}, {"desc", "Patched Valve Wine 9. A replacement for wine-ge, since it's discontinued. No staging patches."},
            {"label", "ValveBE"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-07/unofficial-wine-xiv-valvebe-9-07-clean.tar.zst"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-wayland-9.12"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-wayland-9.12"}, {"desc", "Patched version of Wine Devel 9.12. Includes working wayland session and lsteamclient support."},
            {"label", "Wayland"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v9.12/unofficial-wine-xiv-wayland-9.12.tar.zst"},
            {"mark", "Download"}
        };

        Versions["unofficial-wine-xiv-staging-9.13.1"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-staging-9.13.1"}, {"desc", "Patched version of Wine Staging 9.13. Now with wayland and lsteamclient support added."},
            {"label", "Staging"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v9.13.1/unofficial-wine-xiv-staging-9.13.1.tar.zst"},
            {"mark", "Download"}
        };    

        Versions["unofficial-wine-xiv-git-8.21.1"] = new Dictionary<string, string>()
        {
            {"name", "unofficial-wine-xiv-git-8.21.1"}, {"desc", "Patched version of Wine Staging 8.21 (Last 8.X release). Based on Wine-tkg."},
            {"label", "Staging"}, {"url", "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.21.1/unofficial-wine-xiv-git-8.21.1.tar.xz"},
            {"mark", "Download"}
        };
        
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
                Versions[wineDir.Name] = new Dictionary<string, string>() { {"label", "Custom"} };
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
        if (Versions.ContainsKey("unofficial-wine-xiv-staging-9.13.1"))
            return "unofficial-wine-xiv-staging-9.13.1";
        if (Versions.ContainsKey("wine-xiv-staging-fsync-git-8.5.r4.g4211bac7"))
            return "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";
        if (Versions.ContainsKey("wine-xiv-staging-fsync-git-7.10.r3.g560db77d"))
            return "wine-xiv-staging-fsync-git-7.10.r3.g560db77d";
        return Versions.First().Key;
    }
}