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
    
    static Proton()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();

        Versions["UMU-Proton-9.0-3"] = new Dictionary<string, string>()
        {
            {"name", "UMU Proton 9.0-3"}, {"desc", "UMU-Proton-9.0-3. This is basically Steam's official Proton 9 release."},
            {"label", "UMU-Proton"}, {"url", "https://github.com/Open-Wine-Components/umu-proton/releases/download/UMU-Proton-9.0-3/UMU-Proton-9.0-3.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolSetup.CompatDir.FullName, "UMU-Proton-9.0-3")}
        };

        Versions["GE-Proton9-14"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton 9-14"}, {"desc", "GloriousEggroll's Proton release 9-14. May have mouse warp bug in some xwayland sessions."},
            {"label", "GE-Proton"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton9-14/GE-Proton9-14.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolSetup.CompatDir.FullName, "GE-Proton9-14")}
        };

        Versions["XIV-Proton8-30"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 8-30"}, {"desc", "Patched version of GE-Proton8-30 with Ping plugin support."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton8-30/XIV-Proton8-30.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(ToolSetup.CompatDir.FullName, "XIV-Proton8-30")}
        };

        Versions["XIV-Proton9-14"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 9-14"}, {"desc", "Patched version of GE-Proton9-14 with Ping plugin support"},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton9-14/XIV-Proton9-14.tar.zst"},
            {"mark", "Download"}, {"path", Path.Combine(ToolSetup.CompatDir.FullName, "XIV-Proton9-14")}
        };
    } 

    public static void Initialize()
    {       
        if (ToolSetup.IsSteamInstalled)
        {
            try
            {
                foreach (var dir in ToolSetup.CommonDir.EnumerateDirectories("*Proton*").OrderBy(x => x.Name))
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
                Log.Error(ex, $"Couldn't find any Proton versions in {ToolSetup.CommonDir}. No proton or directory does not exist.");
            }
            try
            {
                foreach (var dir in ToolSetup.CompatDir.EnumerateDirectories().OrderBy(x => x.Name))
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
                Log.Error(ex, $"Couldn't find any Proton versions {ToolSetup.CompatDir}. No proton or directory does not exist.");
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
        if (VersionExists("XIV-Proton9-14"))
            return "XIV-Proton9-14";
        if (VersionExists("UMU-Proton-9.0-3"))
            return "UMU-Proton-9.0-3";
        return Versions.First().Key;
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
}