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

        Versions["GE-Proton8-9"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton8-9"}, {"desc", "GloriousEggroll's Proton release 8-9. Last version without mouse warp bug from KDE 5. Fixed in KDE 6."},
            {"label", "GE-Proton"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton8-9/GE-Proton8-9.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "GE-Proton8-9")}
        };

        Versions["GE-Proton9-9"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton9-9"}, {"desc", "GloriousEggroll's Proton release 9-9. May have mouse warp bug in some xwayland sessions."},
            {"label", "GE-Proton"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton9-9/GE-Proton9-9.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "GE-Proton9-9")}
        };

        Versions["XIV-Proton8-30"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton8-30"}, {"desc", "Patched version of GE-Proton8-30 with Dualsense and Ping plugin support."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton8-30/XIV-Proton8-30.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "XIV-Proton8-30")}
        };

        Versions["XIV-Proton9-7"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton9-7"}, {"desc", "Patched version of GE-Proton9-7 with Dualsense and Ping plugin support"},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton9-7/XIV-Proton9-7.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolBuilder.CompatDir.FullName, "XIV-Proton9-7")}
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