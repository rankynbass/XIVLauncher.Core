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
    public const string DEFAULT = "XIV-Proton 8-30";

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static Proton()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
    } 

    public static void Initialize()
    {
        Versions["UMU-Proton-9.0-3"] = new Dictionary<string, string>()
        {
            {"name", "UMU Proton 9.0-3"}, {"desc", "UMU-Proton-9.0-3. This is basically Steam's official Proton 9 release."},
            {"label", "UMU-Proton"}, {"url", "https://github.com/Open-Wine-Components/umu-proton/releases/download/UMU-Proton-9.0-3/UMU-Proton-9.0-3.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "UMU-Proton-9.0-3")}
        };

        Versions["GE-Proton9-14"] = new Dictionary<string, string>()
        {
            {"name", "GE-Proton 9-14"}, {"desc", "GloriousEggroll's Proton release 9-14. May have mouse warp bug in some xwayland sessions."},
            {"label", "GE-Proton"}, {"url", "https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton9-14/GE-Proton9-14.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "GE-Proton9-14")}
        };

        Versions["XIV-Proton8-30"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 8-30"}, {"desc", "Patched version of GE-Proton8-30 with Ping plugin support."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton8-30/XIV-Proton8-30.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "XIV-Proton8-30")}
        };

        Versions["XIV-Proton9-14"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 9-14"}, {"desc", "Patched version of GE-Proton9-14 with Ping plugin support"},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton9-14/XIV-Proton9-14.tar.zst"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "XIV-Proton9-14")}
        };

        if (Runner.IsSteamInstalled)
        {
            try
            {
                foreach (var dir in Runner.CommonDir.EnumerateDirectories("*Proton*").OrderBy(x => x.Name))
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
                Log.Error(ex, $"Couldn't find any Proton versions in {Runner.CommonDir}. No proton or directory does not exist.");
            }
            try
            {
                foreach (var dir in Runner.CompatDir.EnumerateDirectories().OrderBy(x => x.Name))
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
                Log.Error(ex, $"Couldn't find any Proton versions {Runner.CompatDir}. No proton or directory does not exist.");
            }
        }
    }

    internal static string GetVersion(string? name, bool fullName = true)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return fullName ? Versions[name]["path"] : name;
        return fullName ? Versions[GetDefaultVersion()]["path"] : GetDefaultVersion();
    }

    public static string GetDefaultVersion()
    {
        if (Versions.ContainsKey(DEFAULT))
            return DEFAULT;
        if (Versions.ContainsKey("Proton 9.0"))
            return "Proton 9.0";
        if (Versions.ContainsKey("Proton 9.0 (Beta)"))
            return "Proton 9.0 (Beta)";
        if (Versions.ContainsKey("Proton 8.0"))
            return "Proton 8.0";
        return Versions.First().Key;
    }

    internal static string GetDownloadUrl(string? name)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name].ContainsKey("url") ? Versions[name]["url"] : "";
        return Versions[GetDefaultVersion()].ContainsKey("url") ? Versions[name]["url"] : "";
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