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

public static class Proton
{
    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    private static string HOME => System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

    public static string STEAM => Path.Combine(HOME, ".local", "share", "Steam");

    private static DirectoryInfo commonDir;

    private static DirectoryInfo compatDir;
    
    public static bool IsSteamInstalled { get; private set; }

    static Proton()
    {
        commonDir = new DirectoryInfo(Path.Combine(STEAM, "steamapps", "common"));
        compatDir = new DirectoryInfo(Path.Combine(STEAM, "compatibilitytools.d"));
        try
        {
            if (Directory.Exists(STEAM))
            {
                Log.Verbose($"Steam Root is {STEAM}");
                Log.Verbose($"Steam Common Directory is {commonDir.FullName}");
                Log.Verbose($"Steam Compatibility Tools Directory is {compatDir.FullName}");
                IsSteamInstalled = true;
            }
            else
            {
                throw new DirectoryNotFoundException($"Steam Root directory \"{STEAM}\" does not exist or is not a directory.");
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            Log.Error(ex, "No Steam directory found.");
            IsSteamInstalled = false;
        }

        Initialize();
    }

    public static void Initialize()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();

        Versions["UMU-Proton-9.0-beta16-2"] = new Dictionary<string, string>()
        {
            {"name", "UMU-Proton 9.0 beta 16-2"}, {"desc", "Proton 9 beta with a few patches for UMU."},
            {"label", "Custom"}, {"url", "https://github.com/Open-Wine-Components/umu-proton/releases/download/UMU-Proton-9.0-beta16-2/UMU-Proton-9.0-beta16-2.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(compatDir.FullName, "UMU-Proton-9.0-beta16-2")}
        };

        Versions["GE-Proton8-9"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton8-9"}, {"desc", "GloriousEggroll's Proton release 8-9. Last version without mouse warp bug. Fixed in KDE 6."},
            {"label", "Custom"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton8-9/GE-Proton8-9.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(compatDir.FullName, "GE-Proton8-9")}
        };

        Versions["GE-Proton8-32"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton8-32"}, {"desc", "GloriousEggroll's Proton release 8-32. Has mouse warp bug in some xwayland sessions."},
            {"label", "Custom"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton8-32/GE-Proton8-32.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(compatDir.FullName, "GE-Proton8-32")}
        };
        
        if (IsSteamInstalled)
        {
            try
            {
                foreach (var dir in commonDir.EnumerateDirectories("*Proton*").OrderBy(x => x.Name))
                {
                    if (File.Exists(Path.Combine(dir.FullName,"proton")))
                    {
                        Log.Verbose($"Adding {dir.FullName} to Proton.Versions");
                        Versions[dir.Name] = new Dictionary<string, string>() { {"name", dir.Name}, {"label", "Valve"}, {"path", dir.FullName} };
                    }
                    else
                        Log.Verbose($"{dir.FullName} is not a proton directory. Skipping...");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Error($"Couldn't find any Proton versions in {commonDir}. No proton or directory does not exist.");
            }
            try
            {
                foreach (var dir in compatDir.EnumerateDirectories().OrderBy(x => x.Name))
                {
                    if (File.Exists(Path.Combine(dir.FullName,"proton")))
                    {
                        if (Versions.ContainsKey(dir.Name))
                        {
                            Versions[dir.Name].Remove("mark");
                            Log.Verbose($"{dir.FullName} already exists. Removing download mark.");
                            continue;
                        }
                        Log.Verbose($"Adding {dir.FullName} to Proton.Versions");
                        Versions[dir.Name] = new Dictionary<string, string>() {  {"name", dir.Name}, {"label", "Custom"}, {"path", dir.FullName} };
                    }
                    else
                        Log.Verbose($"{dir.FullName} is not a proton directory. Skipping...");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Error($"Couldn't find any Proton versions {compatDir}. No proton or directory does not exist.");
            }
        }
    }

    public static string GetVersionPath(string? name)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name]["path"];
        return Versions[GetDefaultVersion()]["path"];
    }

    public static string GetDefaultVersion()
    {
        if (VersionExists("Proton 8.0")) return "Proton 8.0";
        return "GE-Proton8-9";
    }

    public static bool VersionExists(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Versions.ContainsKey(name);
    }

    public static string GetDownloadUrl(string? name)
    {
        if (!VersionExists(name))
            return "";

        return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
    }

}