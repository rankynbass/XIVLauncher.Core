using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

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
    public RBWineStartupType StartupType { get; private set; }
    public IWineRelease WineRelease { get; private set; }
    public IToolRelease RuntimeRelease { get; private set; }

    public bool EsyncOn { get; }
    public bool FsyncOn { get; }
    public string DebugVars { get; }
    public FileInfo LogFile { get; }
    public DirectoryInfo Prefix { get; }

    public bool IsProton => StartupType == RBWineStartupType.Proton;
    public bool IsUsingRuntime => (RuntimeRelease != null) && IsProton;
    private string parentPath { get; }
    private string runtimePath => (RuntimeRelease != null) ? Path.Combine(RuntimeRelease.Name, "_v2-entry-point") : "";
    private string runnerPath { get; private set; }

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

    public WineSettings(RBWineStartupType startupType, IWineRelease wineRelease, DirectoryInfo wineFolder, IToolRelease runtime, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool esyncOn, bool fsyncOn)
    {
        this.WineRelease = wineRelease;
        switch (startupType)
        {
            case RBWineStartupType.Custom:
                this.parentPath = wineRelease.Name;
                this.runnerPath = SetWineOrWine64(parentPath);
                this.WineServer = Path.Combine(parentPath, "wineserver");
                this.RuntimeRelease = null;
                break;

            case RBWineStartupType.Managed:
                this.parentPath = Path.Combine(wineRelease.ParentFolder, wineRelease.Name);
                this.runnerPath = SetWineOrWine64(parentPath);
                this.WineServer = Path.Combine(parentPath, "wineserver");
                this.RuntimeRelease = null;
                break;

            case RBWineStartupType.Proton:
                this.parentPath = Path.Combine(wineRelease.ParentFolder, wineRelease.Name);
                this.runnerPath = Path.Combine(parentPath, "proton");
                this.WineServer = Path.Combine(parentPath, "files", "bin", "wineserver");
                this.RuntimeRelease = runtime;
                break;
        }
        this.EsyncOn = esyncOn;
        this.FsyncOn = fsyncOn;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }

    // Some 64-bit wine releases, if 64-bit only, may contain a wine binary but not a wine64 binary.
    public void SetWineOrWine64(string parentPath)
    {
        var wine64 = new FileInfo(parentPath, "wine64");
        var wine = new FileInfo(parentPath, "wine");
        if (wine64.Exists)
            runnerPath = wine64.FullName;
        else if (wine.Exists)
            runnerPath = wine.FullName;
        else
            runnerPath = wine64.FullName;
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
        if (File.Exists(Path.Combine(path, "wine64")))
            return true;
        if (File.Exists(Path.Combine(path, "wine")))
            return true;
        return false;
    }
}
