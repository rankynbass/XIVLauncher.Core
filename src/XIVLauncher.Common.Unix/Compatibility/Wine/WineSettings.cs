using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Unix.Compatibility.Proton;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class WineSettings
{
    public RBWineStartupType StartupType { get; private set; }
    public IWineRelease WineRelease { get; private set; }

    public string CustomBinPath { get; private set; }
    public bool EsyncOn { get; private set; }
    public bool FsyncOn { get; private set; }
    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }
    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(RBWineStartupType startupType, IWineRelease managedWine, string customBinPath, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool esyncOn, bool fsyncOn)
    {
        this.StartupType = (startupType == RBWineStartupType.Proton) ? RBWineStartupType.Managed : startupType;
        this.WineRelease = managedWine;
        this.CustomBinPath = customBinPath;
        this.EsyncOn = esyncOn;
        this.FsyncOn = fsyncOn;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
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
}
