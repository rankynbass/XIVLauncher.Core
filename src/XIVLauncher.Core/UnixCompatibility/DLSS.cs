using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Core;

namespace XIVLauncher.Core.UnixCompatibility;

public static class DLSS
{
    public const string DEFAULT = "dxvk-nvapi-0.7.1";

    public static bool Enabled => IsDLSSAvailable && Program.Config.NvapiVersion != "DISABLED" && Dxvk.Enabled;

    public static string Folder => Enabled ? GetVersion(Program.Config.NvapiVersion) : "";

    public static string DownloadUrl => GetDownloadUrl(Program.Config.NvapiVersion);

    public static string NvngxPath => Enabled ? DLSS.NvidiaWineDLLPath() : "";
    
    private static string? nvngxPath = CoreEnvironmentSettings.ForceDLSS ? "" : CoreEnvironmentSettings.NvngxPath;

    public static bool IsDLSSAvailable => !string.IsNullOrEmpty(NvidiaWineDLLPath()) || CoreEnvironmentSettings.ForceDLSS;

    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }

    static DLSS()
    {
        var dlssStatus = CoreEnvironmentSettings.ForceDLSS ? "forced on with XL_FORCE_DLSS=1" : (IsDLSSAvailable ? $"nvngx.dll found at {NvidiaWineDLLPath()}" : "nvngx.dll not found");
        Log.Information($"DLSS: {dlssStatus}");
        Versions = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize()
    {
        if (IsDLSSAvailable)
        {
            // Default dxvi-nvapi versions. Only add if DLSS is available.
            Versions.Add("dxvk-nvapi-v0.7.1", new Dictionary<string, string>()
            {
                {"name", "0.7.1"}, {"desc", "dxvk-nvapi 0.7.1. Latest version, should be compatible with latest Nvidia drivers." },
                {"label", "Current"}, {"url", "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.7.1/dxvk-nvapi-v0.7.1.tar.gz"},
                {"mark", "download"}
            });
            Versions.Add("dxvk-nvapi-v0.6.4", new Dictionary<string, string>()
            {
                {"name", "0.6.4"}, {"desc", "dxvk-nvapi 0.6.4. Try this if 0.7.1 doesn't work." },
                {"label", "Current"}, {"url", "https://github.com/jp7677/dxvk-nvapi/releases/download/v0.6.4/dxvk-nvapi-v0.6.4.tar.gz"},
                {"mark", "download"}
            });
        }
        Versions.Add("DISABLED", new Dictionary<string, string>()
        {
            {"name", "Disabled"}, {"desc", "Disable native DLSS. Use this for the FSR2/3/XeSS mod."},
            {"label", "DLSS Off"}
        });

        var toolDirectory = new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "dxvk"));

        if (!toolDirectory.Exists)
        {
            Program.storage.GetFolder("compatibitytool/dxvk");
            return;
        }

                foreach (var dxvkDir in toolDirectory.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (Directory.Exists(Path.Combine(dxvkDir.FullName, "x64")) && Directory.Exists(Path.Combine(dxvkDir.FullName, "x32")))
            {
                if (dxvkDir.Name.Contains("nvapi"))
                {
                    // Don't add anything to Nvapi if DLSS is not available.
                    if (!IsDLSSAvailable)
                        continue;
                    if (Versions.ContainsKey(dxvkDir.Name))
                    {
                        Versions[dxvkDir.Name].Remove("mark");
                        continue;
                    }
                    Versions.Add(dxvkDir.Name, new Dictionary<string, string>() { {"label", "Custom"} });
                }
                else
                {
                    continue;
                }
            }
        }
    }

    public static string GetVersion(string? name)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return name;
        return GetDefaultVersion();
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
        if (Versions.ContainsKey(DEFAULT))
            return DEFAULT;
        return Versions.First().Key;
    }

    public static void ReInitialize()
    {
        foreach (var nvapi in Versions)
            Versions.Remove(nvapi.Key);
        Initialize();
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
    
    static public string NvidiaWineDLLPath()
    {
        if (nvngxPath is not null)
        {
            if (!File.Exists(Path.Combine(nvngxPath, "nvngx.dll")))
                nvngxPath = "";
            return nvngxPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string[] targets = { Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".xlcore", "compatibilitytool", "nvidia"), "/usr/lib64", "/usr/lib", "/lib64", "/lib" };
            
            foreach (var target in targets)
            {
                var psi = new ProcessStartInfo("find");
                psi.Arguments = $"{target} -name \"nvngx.dll\"";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                var findCmd = new Process();
                findCmd.StartInfo = psi;
                try
                {
                    findCmd.Start();
                    var output = findCmd.StandardOutput.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var nvngx = new FileInfo(output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
                        nvngxPath = nvngx.DirectoryName;
                        break;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Console.WriteLine("Error: could not execute \"find\" command. Is it installed?");
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    findCmd.Dispose();
                }
            }
            if (string.IsNullOrWhiteSpace(nvngxPath))
                nvngxPath = "";
        }
        else
        {
            nvngxPath = "";
        }
        nvngxPath ??= ""; // If nvngxPath is still null, set it to empty string to prevent an infinite loop.
        return nvngxPath ?? "";
    }
}