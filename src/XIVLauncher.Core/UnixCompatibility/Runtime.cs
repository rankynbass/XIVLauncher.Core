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

public static class Runtime
{
    public const string DEFAULT = "SteamLinuxRuntime_sniper";

    public static string Folder => Runtime.GetVersion(Program.Config.RuntimeVersion);

    public static string DownloadUrl => Runtime.GetDownloadUrl(Program.Config.RuntimeVersion);

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }
   
    private const string SNIPER_RUNTIME = "https://repo.steampowered.com/steamrt3/images/latest-container-runtime-depot/SteamLinuxRuntime_sniper.tar.xz";

    private const string SOLDIER_RUNTIME = "https://repo.steampowered.com/steamrt2/images/latest-container-runtime-depot/SteamLinuxRuntime_soldier.tar.xz";

    public static void Initialize()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();

        Versions["SteamLinuxRuntime_sniper"] = new Dictionary<string, string>()
        {
            {"name", "Sniper"}, {"desc", "Steam sniper runtime container. For use with Proton 8+."},
            {"url", SNIPER_RUNTIME}, {"mark", "Download"}, {"path", Path.Combine(Runner.CommonDir.FullName, "SteamLinuxRuntime_sniper")}
        };
        
        Versions["SteamLinuxRuntime_soldier"] = new Dictionary<string, string>()
        {
            {"name", "Soldier"}, {"desc", "Steam soldier runtime container. May be needed for some Proton 7 versions."},
            {"url", SOLDIER_RUNTIME}, {"mark", "Download"}, {"path", Path.Combine(Runner.CommonDir.FullName, "SteamLinuxRuntime_soldier")}
        };

        Versions["DISABLED"] = new Dictionary<string, string>()
        {
            {"name", "Disabled"}, {"desc", "Don't use a steam runtime. Not recommended."},
            {"path", ""}
        };

        if (Runner.IsSteamInstalled)
        {
            try
            {
                foreach (var dir in Runner.CommonDir.EnumerateDirectories().OrderBy(x => x.Name))
                {
                    if (File.Exists(Path.Combine(dir.FullName,"_v2-entry-point")))
                    {
                        if (Versions.ContainsKey(dir.Name))
                        {
                            Versions[dir.Name].Remove("mark");
                            Log.Verbose($"{dir.FullName} already exists. Removing download mark.");
                            continue;
                        }
                        Log.Verbose($"Adding {dir.FullName} to Proton.Versions");
                        Versions[dir.Name] = new Dictionary<string, string>() { {"name", dir.Name}, {"path", dir.FullName} };
                    }
                    else
                        Log.Verbose($"{dir.FullName} is not a runtime directory. Skipping...");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Error($"Couldn't find any Steam runtimes in {Runner.CommonDir}. No runtimes or directory does not exist.");
            }
        }
    }

    public static string GetVersion(string? name, bool folderOnly = false)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return folderOnly ? name : Versions[name]["path"];
        return folderOnly ? GetDefaultVersion() : Versions[GetDefaultVersion()]["path"];
    }

    public static string GetDefaultVersion()
    {
        if (Versions.ContainsKey(DEFAULT))
            return DEFAULT;
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