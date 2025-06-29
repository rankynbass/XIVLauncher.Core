using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Unix.Compatibility.Proton;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class WineSettings
{
    public IWineRelease WineRelease { get; private set; }

    public bool EsyncOn { get; private set; }
    public bool FsyncOn { get; private set; }
    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }
    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(IWineRelease wineRelease, string debugVars, FileInfo logFile, DirectoryInfo prefix, DirectoryInfo toolsFolder, bool esyncOn, bool fsyncOn)
    {
        if (wineRelease.Label == "Custom")
            this.WineRelease = wineRelease;
        else
        {
            var binFolder = Path.Combine(toolsFolder.FullName, "wine", wineRelease.Name, "bin");
            this.WineRelease = new WineCustomRelease(wineRelease.Label, wineRelease.Description, binFolder,
                                wineRelease.DownloadUrl, wineRelease.lsteamclient, wineRelease.Checksums);
        }
        this.EsyncOn = esyncOn;
        this.FsyncOn = fsyncOn;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }

    public string GetWineBinary(string binFolder)
    {
        if (File.Exists(Path.Combine(binFolder, "wine64")))
            return Path.Combine(binFolder, "wine64");
        if (File.Exists(Path.Combine(binFolder, "wine")))
            return Path.Combine(binFolder, "wine");
        return string.Empty;
    }

    public static bool WineDLLOverrideIsValid(string dlls)
    {
        string[] invalid = { "msquic", "mscoree", "d3d9", "d3d11", "d3d10core", "dxgi" };
        var format = @"^(?:(?:[a-zA-Z0-9_\-\.]+,?)+=(?:n,b|b,n|n|b|d|,|);?)+$";

        if (string.IsNullOrEmpty(dlls)) return true;
        if (invalid.Any(s => dlls.Contains(s))) return false;
        if (Regex.IsMatch(dlls, format)) return true;

        return false;
    }

    public static bool Haslsteamclient(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var parent = new FileInfo(path).Directory.FullName;
        // Arch
        if (File.Exists(Path.Combine(parent, "lib", "wine", "x86_64-windows", "lsteamclient.dll")))
            return true;
        // Fedora
        if (File.Exists(Path.Combine(parent, "lib64", "wine", "x86_64-windows", "lsteamclient.dll")))
            return true;
        // Some Debian/Ubuntu distros/builds
        if (File.Exists(Path.Combine(parent, "lib", "x86_64-linux-gnu", "wine", "x86_64-windows", "lsteamclient.dll")))
            return true;
        return false;
    }

    public static bool IsValidWineBinaryPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var wine = new FileInfo(Path.Combine(path, "wine"));
        var wine64 = new FileInfo(Path.Combine(path, "wine64"));
        if (wine64.Exists || wine.Exists)
            return true;
        return false;
    }
}
