using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core;

public enum WineType
{
    [SettingsDescription("Managed by XIVLauncher", "Choose a patched version of wine made specifically for XIVLauncher")]
    Managed,

    [SettingsDescription("Proton", "Choose a Proton version that is already installed in Steam. Does not support flatpak Steam.")]
    Proton,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public enum WineVersion
{
    [SettingsDescription("Wine-xiv 8.5", "A patched version of Wine-staging 8.5. The current default.")]
    Wine8_5,

    [SettingsDescription("Wine-xiv 7.10", "A legacy patched version of Wine, based on 7.10. A previous default")]
    Wine7_10,
}

public static class WineManager
{
#if WINE_XIV_ARCH_LINUX
    private const string DISTRO = "arch";
#elif WINE_XIV_FEDORA_LINUX
    private const string DISTRO = "fedora";
#else
    private const string DISTRO = "ubuntu";
#endif

    private static string xlcore => Program.storage.Root.FullName;

    public static WineRunner GetSettings()
    {

        switch (Program.Config.WineType ?? WineType.Managed)
        {
            case WineType.Custom:
                return GetWine(Program.Config.WineBinaryPath ?? "/usr/bin");
            
            case WineType.Managed:
                return GetWine();

            case WineType.Proton:
                return GetProton();

            default:
                throw new ArgumentOutOfRangeException("Bad value for WineType");
        }

    }

    private static WineRunner GetWine(string runCmd = "")
    {
        var runArgs = "";
        var folder = "";
        var url = "";
        var version = Program.Config.WineVersion ?? WineVersion.Wine8_5;

        switch (version)
        {
            case WineVersion.Wine8_5:
                folder = "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";
                url = $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{DISTRO}-8.5.r4.g4211bac7.tar.xz";
                break;

            case WineVersion.Wine7_10:
                folder = "wine-xiv-staging-fsync-git-7.10.r3.g560db77d";
                url = $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{DISTRO}-7.10.r3.g560db77d.tar.xz";
                break;

            default:
                throw new ArgumentOutOfRangeException("Bad value for WineVersion");
        }

        var env = new Dictionary<string, string>();
        if (Program.Config.GameModeEnabled ?? false)
        {
            var ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";
            if (!ldPreload.Contains("libgamemodeauto.so.0"))
                ldPreload = (ldPreload.Equals("")) ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
            env.Add("LD_PRELOAD", ldPreload);
        }
        if (!string.IsNullOrEmpty(Program.Config.WineDebugVars))
            env.Add("WINEDEBUG", Program.Config.WineDebugVars);
        if (Program.Config.ESyncEnabled ?? true) env.Add("WINEESYNC", "1");
        if (Program.Config.FSyncEnabled ?? false) env.Add("WINEFSYNC", "1");
        env.Add("WINEPREFIX", Path.Combine(xlcore, "wineprefix"));
        
        return new WineRunner(runCmd, runArgs, folder, url, xlcore, env);
    }

    private static WineRunner GetProton()
    {
        var proton = Path.Combine(ProtonManager.GetVersionPath(Program.Config.ProtonVersion), "proton");
        var runCmd = proton;
        var runArgs = "";
        var minRunCmd = "";
#if !FLATPAK
        if (Program.Config.SteamRuntime != "Disabled")
        {
            runCmd = Path.Combine(ProtonManager.GetRuntimePath(Program.Config.SteamRuntime), "_v2-entry-point");
            runArgs = "--verb=waitforexitandrun -- \"" + proton + "\"";
            minRunCmd = proton;
        }
#endif
        var env = new Dictionary<string, string>();
        if (Program.Config.GameModeEnabled ?? false)
        {
            var ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";
            if (!ldPreload.Contains("libgamemodeauto.so.0"))
                ldPreload = (ldPreload.Equals("")) ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
            env.Add("LD_PRELOAD", ldPreload);
        }
        if (!string.IsNullOrEmpty(Program.Config.WineDebugVars))
            env.Add("WINEDEBUG", Program.Config.WineDebugVars);
        env.Add("STEAM_COMPAT_DATA_PATH", Path.Combine(xlcore, "protonprefix"));
        env.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", Program.Config.SteamPath);

        var compatMounts = Environment.GetEnvironmentVariable("STEAM_COMPAT_MOUNTS") ?? "";
        var protonCompatMounts = Program.Config.GamePath + ":" + Program.Config.GameConfigPath;

#if !FLATPAK
        // Extra Steam compatibility mounts for discord ipc bridge
        var discordIPCPaths = "";
        if (Program.Config.SteamRuntime != "Disabled")
        {
            string runPath = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            for (int i = 0; i < 10; i++)
                discordIPCPaths += $"{runPath}/discord-ipc-{i}:{runPath}/app/com.discordapp.Discord/discord-ipc-{i}:{runPath}/snap.discord-cananry/discord-ipc-{i}:";
        }
#endif
        compatMounts = discordIPCPaths + protonCompatMounts + (compatMounts.Equals("") ? "" : ":" + compatMounts);
        env.Add("STEAM_COMPAT_MOUNTS", compatMounts);
        env.Add("WINEPREFIX", Path.Combine(xlcore, "protonprefix", "pfx"));
        if (!Program.Config.FSyncEnabled.Value)
        {
            if (!Program.Config.ESyncEnabled.Value)
                env.Add("PROTON_NO_ESYNC", "1");
            env.Add("PROTON_NO_FSYNC", "1");
        }

        return new WineRunner(runCmd, runArgs, "", "", xlcore, env, isProton: true, minRunCmd: minRunCmd);
    }
}




