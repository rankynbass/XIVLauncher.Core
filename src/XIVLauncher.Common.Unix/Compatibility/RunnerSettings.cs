using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace XIVLauncher.Common.Unix.Compatibility;

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

public class RunnerSettings
{
    public bool IsProton { get; private set; }

    public bool IsUsingRuntime => !string.IsNullOrEmpty(RuntimePath);

    public string WineServerPath { get; private set; }

    public string RunnerPath { get; private set; }

    private string ParentPath;

    public string DownloadUrl;

    private string RuntimePath;

    public string RuntimeUrl;

    private string ExtraOverrides;

    public bool EsyncOn { get; private set; }

    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    private const string WINEDLLOVERRIDES = "msquic=,mscoree=n,b;d3d9,d3d10core,d3d11,dxgi=";

    // Command will be the steam linux runtime if used, or wine/proton if not used.
    public string Command => string.IsNullOrEmpty(RuntimePath) ? RunnerPath : RuntimePath;

    // Run and RunInPrefix will be set to the appropriate values if using proton (with or without container), or "" if using wine.
    public string RunVerb => IsProton ? "run " : "";

    public string RunInPrefixVerb => IsProton ? "runinprefix " : "";

    // RunInRuntimeArguments and RunInRuntimeArgumentsArray will be used if we're using a container. Otherwise set to "".
    public string RunInRuntimeArguments => IsUsingRuntime ? $"--verb=waitforexitandrun -- \"{RunnerPath}\" " : "";

    public string[] RunInRuntimeArgumentsArray => IsUsingRuntime ? new string[] {"--verb=waitforexitandrun", "--", RunnerPath} : new string[] { };

    /*  
        The end result of the above variables is that we will build the process commands as follows:
        Process: Command
        Arguements: RunInRuntimeArguments + RunnerPath + Run/RunInPrefix + command.

        If wine, that'll look like: /path/to/wine64 command
        If proton, it'll look like: /path/to/proton runinprefix command
        If steam runtime, it'll be: /path/to/runtime --verb=waitforexitandrun -- /path/to/proton runinprefix command
    */

    public Dictionary<string, string> Environment { get; private set; }

    internal string SteamCompatMounts { get; private set; }

    // Constructor for Wine
    public RunnerSettings(string runnerPath, string downloadUrl, string extraOverrides, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        IsProton = false;
        ParentPath = WineCheck(runnerPath);
        RunnerPath = File.Exists(Path.Combine(ParentPath, "wine64")) ? Path.Combine(ParentPath, "wine64") : Path.Combine(ParentPath, "wine");
        WineServerPath = Path.Combine(ParentPath, "wineserver");
        DownloadUrl = downloadUrl;
        RuntimePath = "";
        RuntimeUrl = "";
        ExtraOverrides = WINEDLLOVERRIDEIsValid(extraOverrides) ? ";" + extraOverrides : "";
        EsyncOn = esyncOn ?? false;
        FsyncOn = fsyncOn ?? false;
        DebugVars = debugVars;
        LogFile = logFile;
        Prefix = prefix;
        Environment = new Dictionary<string, string>
        {
            {"WINEESYNC", EsyncOn ? "1" : "0"},
            {"WINEFSYNC", FsyncOn ? "1" : "0"},
            {"WINEPREFIX", Prefix.FullName},
            {"XL_WINEONLINUX", "true"}
        };
        if (!string.IsNullOrEmpty(DebugVars))
        {
            Environment.Add("WINEDEBUG", DebugVars);
        }
    }

    // Constructor for Proton
    public RunnerSettings(string runnerPath, string downloadUrl, string runtimePath, string runtimeUrl, string extraOverrides, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        IsProton = true;
        ParentPath = runnerPath;
        RunnerPath = Path.Combine(ParentPath, "proton");
        WineServerPath = Path.Combine(ParentPath, "files", "bin", "wineserver");
        DownloadUrl = downloadUrl;
        RuntimePath = string.IsNullOrEmpty(runtimePath) ? "" : Path.Combine(runtimePath, "_v2-entry-point");
        RuntimeUrl = runtimeUrl;       
        ExtraOverrides = WINEDLLOVERRIDEIsValid(extraOverrides) ? ";" + extraOverrides : "";
        EsyncOn = esyncOn ?? false;
        FsyncOn = fsyncOn ?? false;
        DebugVars = debugVars;
        LogFile = logFile;
        Prefix = prefix;

        Environment = new Dictionary<string, string>
        {
            {"STEAM_COMPAT_DATA_PATH", Prefix.FullName},
            {"XL_WINEONLINUX", "true"}
        };
        if (!FsyncOn)
        {
            Environment.Add("PROTON_NO_FSYNC", "1");
            if (!EsyncOn)
                Environment.Add("PROTON_NO_ESYNC", "1");
        }
        if (!string.IsNullOrEmpty(DebugVars))
        {
            Environment.Add("WINEDEBUG", DebugVars);
        }
    }

    internal void SetWineD3D(bool dxvkEnabled)
    {
        if (!dxvkEnabled)
            Environment.Add("PROTON_USE_WINED3D", "1");
    }

    internal void SetSteamFolder(string steamFolder)
    {
        Environment.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", steamFolder);
    }

    internal void SetSteamCompatMounts(string gamefolder, string configfolder)
    {
        var importantPaths = new System.Text.StringBuilder($"{gamefolder}:{configfolder}");
        var steamCompatMounts = System.Environment.GetEnvironmentVariable("STEAM_COMPAT_MOUNTS");
        if (!string.IsNullOrEmpty(steamCompatMounts))
            importantPaths.Append(":" + steamCompatMounts.Trim(':'));
        
        // These paths are for winediscordipcbridge.exe. Note that exact files are being passed, not directories.
        // You can't pass the whole /run/user/<userid> directory; it will get ignored.
        var runtimeDir = System.Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrEmpty(runtimeDir))
        {
            // If XDG_RUNTIME_DIR isn't set, discord ipc won't work, so there's no point in continuing.
            SteamCompatMounts = importantPaths.ToString();
            return;
        }
        for (int i = 0; i < 10; i++)
            importantPaths.Append($":{runtimeDir}/discord-ipc-{i}");
        importantPaths.Append($"{runtimeDir}/app/com.discordapp.Discord:{runtimeDir}/snap.discord-canary");
        
        Environment.Add("STEAM_COMPAT_MOUNTS", importantPaths.ToString());
    }

    internal string GetWineDLLOverrides(bool dxvk)
    {
        return WINEDLLOVERRIDES + (dxvk ? "n,b" : "b")  + ExtraOverrides;
    }

    private string WineCheck(string dir)
    {
        var directory = new DirectoryInfo(dir);
        if (directory.Name == "bin")
            return directory.FullName;
        return Path.Combine(directory.FullName, "bin");            
    }

    public static bool WINEDLLOVERRIDEIsValid(string dlls)
    {
        string[] invalid = { "msquic", "mscoree", "d3d9", "d3d11", "d3d10core", "dxgi" };
        var format = @"^(?:(?:[a-zA-Z0-9_\-\.]+,?)+=(?:n,b|b,n|n|b|d|,|);?)+$";

        if (string.IsNullOrEmpty(dlls)) return true;
        if (invalid.Any(s => dlls.Contains(s))) return false;
        if (Regex.IsMatch(dlls, format)) return true;

        return false;
    }
}