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
    
    public static void Initialize()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();

        Versions["UMU-Proton-9.0-beta16-2"] = new Dictionary<string, string>()
        {
            {"name", "UMU-Proton 9.0 beta 16-2"}, {"desc", "Proton 9 beta with a few patches for UMU."},
            {"label", "Custom"}, {"url", "https://github.com/Open-Wine-Components/umu-proton/releases/download/UMU-Proton-9.0-beta16-2/UMU-Proton-9.0-beta16-2.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "UMU-Proton-9.0-beta16-2")}
        };

        Versions["GE-Proton8-9"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton8-9"}, {"desc", "GloriousEggroll's Proton release 8-9. Last version without mouse warp bug. Fixed in KDE 6."},
            {"label", "Custom"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton8-9/GE-Proton8-9.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "GE-Proton8-9")}
        };

        Versions["GE-Proton8-32"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton8-32"}, {"desc", "GloriousEggroll's Proton release 8-32. Has mouse warp bug in some xwayland sessions."},
            {"label", "Custom"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton8-32/GE-Proton8-32.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "GE-Proton8-32")}
        };
        
        if (ToolBuilder.IsSteamInstalled)
        {
            try
            {
                foreach (var dir in ToolBuilder.CommonDir.EnumerateDirectories("*Proton*").OrderBy(x => x.Name))
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
                Log.Error($"Couldn't find any Proton versions in {ToolBuilder.CommonDir}. No proton or directory does not exist.");
            }
            try
            {
                foreach (var dir in ToolBuilder.CompatDir.EnumerateDirectories().OrderBy(x => x.Name))
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
                Log.Error($"Couldn't find any Proton versions {ToolBuilder.CompatDir}. No proton or directory does not exist.");
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
        if (!VersionExists(name)) return "";
        return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
    }
}