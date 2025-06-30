using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Unix.Compatibility.Proton;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

/*
To successfully launch dalamud/ffxiv in steam linux runtime container with proton, the following environtment variables *must* be set:

    STEAM_COMPAT_DATA_PATH, which is the proton prefix. The *wine* prefix is STEAM_COMPAT_DATA_PATH/pfx, which we symlink back to STEAM_COMPAT_DATA_PATH
    We do *not* need to set WINEPREFIX directly; Proton will take care of that.
    
    STEAM_COMPAT_CLIENT_INSTALL_PATH, which is the location of the steam install in the user's profile. Usually $HOME/.local/share/Steam. If this is not set
    during prefix creation/refresh, the resulting prefix will be *broken*, and it will be impossible to launch programs using proton.

These environment variables are needed in some cases:

    STEAM_COMPAT_MOUNTS, a colon separated list of directories to be allowed inside the container. If ffxiv or ffxivConfig are not in the user's home folder,
    this needs to include those paths or they wont be visible from the container. This is basically the Steam equivalent of flatseal file permissions.
    You *cannot* pass certain directories. /bin, /usr/bin, /lib, /usr/lib, /run, /run/user/<userid>, /proc, and a few others can't be passed. You *can* pass
    specific files inside those directories, however.
    
    STEAM_COMPAT_INSTALL_PATH is supposed to point to the game's install directory. However, we're just using STEAM_COMPAT_MOUNTS for everything instead,
    because if we launch from Steam, this might automatically get set for us.

    PRESSURE_VESSEL_FILESYSTEMS_RW is another way to pass files and directories into the container. Again, we're just using STEAM_COMPAT_MOUNTS.

    WINEDLLOVERRIDES to specify that dxvk should be used, just like with wine. This is supposed to be set by proton, but it doesn't work properly, so we set
    it manually.
    
    PROTON_NO_FSYNC and PROTON_NO_ESYNC need to be set if esync/fsync are disabled. These options are exactly opposite of the wine equivalents, WINEESYNC
    and WINEFSYNC. Proton will set WINEESYNC and WINEFSYNC for us. These need to be set during EnsurePrefix.

The actual command that gets run looks like this:

    "$HOME/.local/share/Steam/steamapps/common/SteamLinuxRuntime_sniper/_v2-entry-point" --verb=waitforexitandrun -- "$HOME/.local/share/Steam/compatibilitytools.d/GE-Proton8-9/proton" runinprefix <command>

Without using the runtime, it looks like this:

    "$HOME/.local/share/Steam/compatibilitytools.d/GE-Proton8-9/proton" runinprefix <command>

When ensuring the prefix, we use "run" instead of "runinprefix" because the first command will make sure the prefix is set up before returning, while the
second assumes the prefix is already set up.

Proton has built-in commands for doing unix->windows path converions. We don't use it because it is just a wrapper for the equivalent wine commands, and it
uses 32-bit wine when we can't assume 32-bit support will be available (for example in flatpak releases).
*/

public class WineSettings
{
    public IWineRelease WineRelease { get; private set; }

    public bool EsyncOn { get; }
    public bool FsyncOn { get; }
    public string DebugVars { get; }
    public FileInfo LogFile { get; }
    public DirectoryInfo Prefix { get; }

    public bool IsProton { get; }
    public bool IsUsingRuntime => runtimePath != null;
    private string parentPath { get; }
    private string runtimePath { get; }
    private string runnerPath { get; }

    public string Command => IsUsingRuntime ? runtimePath : runnerPath;
    public string AltCommand => runnerPath;
    public string WineServer { get; private set; }
    public string RunVerb => IsProton ? "run " : "";
    public string RunInPrefixVerb => IsProton ? "runinprefix " : "";
    public string RunInRuntimeArgs => IsUsingRuntime ? $"--verb=waitforexitandrun -- \"{runnerPath}\" " : "";
    public string RunInRuntimeArgsArray => IsUsingRuntime ? [ "--verb=waitforexitandrun", "--", runnerPath ] : new string[] { };

    /*  
        The end result of the above variables is that we will build the process commands as follows:
        Process: Command
        Arguements: RunInRuntimeArguments + RunnerPath + Run/RunInPrefix + command.

        If wine, that'll look like: /path/to/wine64 command
        If proton, it'll look like: /path/to/proton runinprefix command
        If steam runtime, it'll be: /path/to/runtime --verb=waitforexitandrun -- /path/to/proton runinprefix command
    */

    public WineSettings(IWineRelease wineRelease, string debugVars, FileInfo logFile, DirectoryInfo prefix, DirectoryInfo wineFolder, bool esyncOn, bool fsyncOn)
    {
        this.WineRelease = wineRelease;
        this.parentPath = (wineRelease.Label == "CUSTOM") ? WineRelease.Name : Path.Combine(wineFolder, wineRelease.Name, "bin");
        this.runnerPath = GetBinary(parentPath);
        this.WineServer = Path.Combine(parentPath, "wineserver");
        this.runtimePath = null;
        this.EsyncOn = esyncOn;
        this.FsyncOn = fsyncOn;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
        this.IsProton = false;
        this.IsUsingRuntime = false;
    }

    public WineSettings(IToolRelease protonRelease, string protonFolder, string runtimePath, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool esyncOn, bool fsyncOn)
    {
        this.WineRelease = new WineCustomRelease(protonRelease.Label, protonRelease.Description, protonRelease.Name, protonRelease.DownloadUrl, true, [ protonRelease.Checksum ]);
        this.parentPath = protonFolder;
        this.WineServer = Path.Combine(protonFolder, "files", "bin", "wineserver");
        this.runnerPath = Path.Combine(protonFolder, WineRelease.Name, "proton");
        this.runtimePath = string.IsNullOrEmpty(runtimePath) ? null : Path.Combine(runtimePath, "_v2-entry-point");
        this.IsProton = true;
        this.EsyncOn = esyncOn;
        this.FsyncOn = fsyncOn;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }

    private string GetBinary(string binFolder)
    {
        if (IsProton)
        {
            if (File.Exists(Path.Combine(binFolder, "proton")))
                return Path.Combine(binFolder, "proton");
        }
        else
        {
            if (File.Exists(Path.Combine(binFolder, "wine64")))
                return Path.Combine(binFolder, "wine64");
            if (File.Exists(Path.Combine(binFolder, "wine")))
                return Path.Combine(binFolder, "wine");
        }
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
