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
    public const string DEFAULT = "XIV-Proton10-1";

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static Proton()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
    } 

    public static void Initialize()
    {
        Versions["xiv-proton-9.0-4"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 9.0-4"}, {"desc", "XIV-Proton-9.0-4. This is basically Steam's official Proton 9 release with XIV patches."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/xiv-proton-9.0-4/xiv-proton-9.0-4.tar.xz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "xiv-proton-9.0-4")}
        };

        Versions["XIV-Proton8-30"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 8-30"}, {"desc", "Patched version of GE-Proton8-30 with Ping plugin support."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton8-30/XIV-Proton8-30.tar.gz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "XIV-Proton8-30")}
        };

        Versions["XIV-Proton9-27"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 9-27"}, {"desc", "Patched version of GE-Proton9-27 with Ping plugin support. Patched for 7.2 Dalamud."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton9-27/XIV-Proton9-27.tar.xz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "XIV-Proton9-27")}
        };

        Versions["XIV-Proton10-1"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 10-1"}, {"desc", "Patched version of GE-Proton10-1 (Ping fix is now upstream). Patched for 7.2 Dalamud."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton10-1/XIV-Proton10-1.tar.xz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "XIV-Proton10-1")}
        };

        Versions["XIV-Proton10-1-ntsync"] = new Dictionary<string, string>()
        {
            {"name", "XIV-Proton 10-1 NTSync"}, {"desc", "Patched version of GE-Proton10-1 with NTSync (Ping fix is now upstream). Patched for 7.2 Dalamud."},
            {"label", "XIV-patched"}, {"url", "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton10-1/XIV-Proton10-1-ntsync.tar.xz"},
            {"mark", "Download"}, {"path", Path.Combine(Runner.CompatDir.FullName, "XIV-Proton10-1-ntsync")}
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
                        string label;
                        string dirname = dir.Name.ToLower();
                        if (dirname.StartsWith("xiv-proton"))
                            label = "XIV-patched";
                        else if (dirname.StartsWith("ge-proton"))
                            label = "GE-Proton";
                        else if (dirname.StartsWith("umu-proton"))
                            label = "UMU-Proton";
                        else
                            label = "Custom";

                        Log.Verbose($"Adding {dir.FullName} to Proton.Versions");
                        Versions[dir.Name] = new Dictionary<string, string>() {  {"name", dir.Name}, {"label", label}, {"path", dir.FullName} };
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