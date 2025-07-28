using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class WineSettings
{
    private const string WINEDLLOVERRIDES = "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=";

    public IWineRelease WineRelease { get; private set; }
    public IToolRelease RuntimeRelease { get; private set; }

    public bool EsyncOn { get; }
    public bool FsyncOn { get; }
    public bool NTSyncOn { get; }
    public bool WaylandOn { get; }
    public string WineDLLOverrides { get; private set; }
    public string DebugVars { get; }
    public FileInfo LogFile { get; }
    public DirectoryInfo Prefix { get; }
    public XLCorePaths Paths { get; }

    public bool IsProton => WineRelease.IsProton;
    public bool IsUsingRuntime => IsProton; //(RuntimeRelease != null) && IsProton;
    private string parentPath { get; }
    public string WinePath { get; private set; }
    public string WineServerPath { get; private set; }

    public Dictionary<string, string> EnvVars { get; private set; }



    /*  
        The end result of the above variables is that we will build the process commands as follows:
        Process: Command
        Arguements: RunInRuntimeArguments + WinePath + Run/RunInPrefix + command.

        If wine, that'll look like: /path/to/wine64 command
        If proton, it'll look like: /path/to/proton runinprefix command
        If steam runtime, it'll be: /path/to/runtime --verb=waitforexitandrun -- /path/to/proton runinprefix command
    */

    public WineSettings(IWineRelease wineRelease, IToolRelease runtime, string dlloverrides, XLCorePaths paths, string debugVars, FileInfo logFile, bool esyncOn, bool fsyncOn, bool ntsyncOn, bool waylandOn)
    {
        this.WineRelease = wineRelease;
        if (wineRelease.IsProton)
        {
            this.parentPath = (wineRelease.Label == "CUSTOM") ? wineRelease.Name : Path.Combine(wineRelease.ParentFolder, wineRelease.Name);
            this.WinePath = Path.Combine(parentPath, "proton");
            this.WineServerPath = Path.Combine(parentPath, "files", "bin", "wineserver");
            this.RuntimeRelease = runtime;
        }
        else
        {
            this.parentPath = (wineRelease.Label == "CUSTOM") ? wineRelease.Name : Path.Combine(wineRelease.ParentFolder, wineRelease.Name, "bin");
            this.SetWineOrWine64(parentPath);
            this.WineServerPath = Path.Combine(parentPath, "wineserver");
            this.RuntimeRelease = null;
        }
        this.EsyncOn = esyncOn;
        this.FsyncOn = fsyncOn;
        this.NTSyncOn = ntsyncOn;
        this.WaylandOn = waylandOn;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = paths.Prefix;
        this.Paths = paths;
        this.WineDLLOverrides = (WineSettings.WineDLLOverrideIsValid(dlloverrides) && !string.IsNullOrEmpty(dlloverrides) ? dlloverrides + ";" : "") + WINEDLLOVERRIDES;
        this.EnvVars = new Dictionary<string, string>();
        if (IsProton)
        {
            if (!IsUsingRuntime)
            {
                EnvVars.Add("STEAM_COMPAT_DATA_PATH", Prefix.FullName);
                EnvVars.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", Paths.SteamFolder.FullName);
            }
            else
            {
                EnvVars.Add("WINEPREFIX", Paths.SteamFolder.FullName);
                EnvVars.Add("PROTONPATH", parentPath);
                EnvVars.Add("STORE", "none");
                EnvVars.Add("PROTON_VERB", "runinprefix");
            }
            if (!NTSyncOn)
                EnvVars.Add("PROTON_NO_NTSYNC", "1");
            else if (WineRelease.Name == "GE-Proton10-9")
                EnvVars.Add("PROTON_USE_NTSYNC", "1");

            if (!FsyncOn)
            {
                EnvVars.Add("PROTON_NO_FSYNC", "1");
                if (!EsyncOn)
                    EnvVars.Add("PROTON_NO_ESYNC", "1");
            }

            if (WaylandOn)
                EnvVars.Add("PROTON_ENABLE_WAYLAND", "1");

            setSteamCompatMounts();
        }
        else
        {
            EnvVars.Add("WINEESYNC", EsyncOn ? "1" : "0");
            EnvVars.Add("WINEFSYNC", FsyncOn ? "1" : "0");
            EnvVars.Add("WINENTSYNC", NTSyncOn ? "1" : "0");
            EnvVars.Add("WINEPREFIX", Prefix.FullName);
            if (WaylandOn)
                EnvVars.Add("DISPLAY", null);
        }
    }

    private void setSteamCompatMounts()
    {
        var importantPaths = new System.Text.StringBuilder($"{Paths.GameFolder.FullName}:{Paths.ConfigFolder.FullName}");
        var steamCompatMounts = System.Environment.GetEnvironmentVariable("STEAM_COMPAT_MOUNTS");
        if (!string.IsNullOrEmpty(steamCompatMounts))
            importantPaths.Append(":" + steamCompatMounts.Trim(':'));
        
        // These paths are for winediscordipcbridge.exe. Note that exact files are being passed, not directories.
        // You can't pass the whole /run/user/<userid> directory; it will get ignored, so we pass all 10 potential
        // values for /run/user/<userid>/discord-ipc-{0-9}
        // Flatpak and snap will both use /run/user/<userid> for their XDG_RUNTIME_DIR, so this will work inside flatpak/snap steam
        var runtimeDir = System.Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrEmpty(runtimeDir))
        {
            for (int i = 0; i < 10; i++)
                importantPaths.Append($":{runtimeDir}/discord-ipc-{i}");
            importantPaths.Append($"{runtimeDir}/app/com.discordapp.Discord:{runtimeDir}/snap.discord-canary");
        }       
        EnvVars.Add("STEAM_COMPAT_MOUNTS", importantPaths.ToString());
    }

    // Some 64-bit wine releases, if 64-bit only, may contain a wine binary but not a wine64 binary.
    public void SetWineOrWine64(string parentPath)
    {
        var wine64 = new FileInfo(Path.Combine(parentPath, "wine64"));
        var wine = new FileInfo(Path.Combine(parentPath, "wine"));
        if (wine64.Exists)
            WinePath = wine64.FullName;
        else if (wine.Exists)
            WinePath = wine.FullName;
        else
            WinePath = wine64.FullName;
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

    public static bool HasLsteamclient(string? path)
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
        if (File.Exists(Path.Combine(path, "wine64")))
            return true;
        if (File.Exists(Path.Combine(path, "wine")))
            return true;
        return false;
    }

    public static bool IsValidProtonBinaryPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        if (File.Exists(Path.Combine(path, "proton")))
            return true;
        return false;
    }
}
