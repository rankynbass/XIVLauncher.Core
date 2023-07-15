using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Core;

namespace XIVLauncher.Core;

public enum WineType
{
    [SettingsDescription("Managed by XIVLauncher", "Choose a patched version of wine made specifically for XIVLauncher")]
    Managed,

    [SettingsDescription("Rankyn's XIV-patched Wine-tkg", "Unoffical Wine-tkg builds with XIV patches. Build on Ubuntu 20.04.")]
    RB_Wine,

    [SettingsDescription("Rankyn's XIV-patched Wine-GE", "Unofficial Wine-proton builds with XIV patches build with the Wine-GE build script")]
    RB_Proton,

    [SettingsDescription("Proton", "Choose a Proton version that is already installed in Steam. Does not support flatpak Steam.")]
    Proton,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public enum WineVersion
{
    [SettingsDescription("Wine-xiv 7.10", "A legacy patched version of Wine, based on 7.10. A previous default")]
    Wine7_10,

    [SettingsDescription("Wine-xiv 8.5", "A patched version of Wine-staging 8.5. The current default.")]
    Wine8_5,
}

public enum RBWineVersion
{
    [SettingsDescription("v8.12", "Unofficial version of Wine-staging 8.12 with WoW64. No 32-bit libs needed, Wine as Win10 supported.")]
    Wine8_12,

    [SettingsDescription("v8.11", "Unofficial version of Wine-staging 8.11. Wine as Win10 supported.")]
    Wine8_11,

    [SettingsDescription("v8.10", "Unofficial version of Wine-staging 8.10. Set windows version to 7.")]
    Wine8_10,

    [SettingsDescription("v8.8", "Unofficial version of Wine-staging 8.8. Set windows version to 7.")]
    Wine8_8,

    [SettingsDescription("v7.22", "Unofficial version of Wine-staging 7.22. Last version of Wine 7. Set windows version to 7.")]
    Wine7_22,
}

public enum RBProtonVersion
{
    [SettingsDescription("xiv-Proton8-10", "Unofficial version of Wine-GE Proton8-10 with XIV patches.")]
    Proton8_10,

    [SettingsDescription("xiv-Proton8-8", "Unofficial version of Wine-GE Proton8-8 with XIV patches.")]
    Proton8_8,

    [SettingsDescription("xiv-Proton8-4", "Unofficial version of Wine-GE Proton8-4 with XIV patches.")]
    Proton8_4,

    [SettingsDescription("xiv-Proton7-43", "Unofficial version of Wine-GE Proton7-43 with XIV patches. Last GE-7 release.")]
    Proton7_43,

    [SettingsDescription("xiv-Proton7-35", "Unofficial version of Wine-GE Proton7-35 with XIV patches. First with DS patch.")]
    Proton7_35,
}

public static class WineManager
{
    private static string xlcore => Program.storage.Root.FullName;

    public static WineSettings GetSettings()
    {

        switch (Program.Config.WineType ?? WineType.Managed)
        {
            case WineType.Custom:
                return GetWine(Program.Config.WineBinaryPath ?? "/usr/bin");
            
            case WineType.Managed:
            case WineType.RB_Proton:
            case WineType.RB_Wine:
                return GetWine();

            case WineType.Proton:
                return GetProton();

            default:
                throw new ArgumentOutOfRangeException("Bad value for WineType");
        }

    }

    private static WineSettings GetWine(string runCmd = "")
    {
        var winepath = runCmd;
        var folder = "";
        var url = "";
        var package = Distro.Package.ToString();
    
        if (Program.Config.WineType == WineType.Managed)
        {
            var version = Program.Config.WineVersion ?? WineVersion.Wine7_10;

            switch (version)
            {
                case WineVersion.Wine8_5:
                    folder = "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";
                    url = $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{package}-8.5.r4.g4211bac7.tar.xz";
                    break;

                case WineVersion.Wine7_10:
                    folder = "wine-xiv-staging-fsync-git-7.10.r3.g560db77d";
                    url = $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{package}-7.10.r3.g560db77d.tar.xz";
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Bad value for WineVersion");
            }
        }
        else if (Program.Config.WineType == WineType.RB_Wine)
        {
            var version = Program.Config.RBWineVersion ?? RBWineVersion.Wine8_12;
            switch (version)
            {
                case RBWineVersion.Wine8_12:
                    folder = "unofficial-wine-xiv-git-8.12.0";
                    url = "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.12.0/unofficial-wine-xiv-git-8.12.0.tar.xz";
                    break;

                case RBWineVersion.Wine8_11:
                    folder = "unofficial-wine-xiv-git-8.11.0";
                    url = "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.11.0/unofficial-wine-xiv-git-8.11.0.tar.xz";
                    break;

                case RBWineVersion.Wine8_10:
                    folder = "unofficial-wine-xiv-git-8.10";
                    url = "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.10/unofficial-wine-xiv-git-8.10.tar.xz";
                    break;

                case RBWineVersion.Wine8_8:
                    folder = "unofficial-wine-xiv-git-8.8.0";
                    url = "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.8.0/unofficial-wine-xiv-git-8.8.0.tar.xz";
                    break;

                case RBWineVersion.Wine7_22:
                    folder = "unofficial-wine-xiv-git-7.22.0";
                    url = "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v7.22.0/unofficial-wine-xiv-git-7.22.0.tar.xz";
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Bad value for RBWineVersion");            
            }
        }
        else if (Program.Config.WineType == WineType.RB_Proton)
        {
            var version = Program.Config.RBProtonVersion ?? RBProtonVersion.Proton8_10;

            switch (version)
            {
                case RBProtonVersion.Proton8_10:
                    folder = "unofficial-wine-xiv-Proton8-10-x86_64";
                    url = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-10/unofficial-wine-xiv-Proton8-10-x86_64.tar.xz";
                    break;

                case RBProtonVersion.Proton8_8:
                    folder = "unofficial-wine-xiv-Proton8-8-x86_64";
                    url = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-8/unofficial-wine-xiv-Proton8-8-x86_64.tar.xz";
                    break;

                case RBProtonVersion.Proton8_4:
                    folder = "unofficial-wine-xiv-Proton8-4-x86_64";
                    url = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-4/unofficial-wine-xiv-Proton8-4-x86_64.tar.xz";
                    break;

                case RBProtonVersion.Proton7_43:
                    folder = "unofficial-wine-xiv-Proton7-43-x86_64";
                    url = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-43/unofficial-wine-xiv-Proton7-43-x86_64.tar.xz";
                    break;

                case RBProtonVersion.Proton7_35:
                    folder = "unofficial-wine-xiv-Proton7-35-x86_64";
                    url = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-35/unofficial-wine-xiv-Proton7-35-x86_64.tar.xz";
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Bad value for RBProtonVersion");
            }
        }

        var env = new Dictionary<string, string>();
        if (Program.Config.GameModeEnabled ?? false)
            env.Add("LD_PRELOAD", "libgamemodeauto.so.0");
        if (!string.IsNullOrEmpty(Program.Config.WineDebugVars))
            env.Add("WINEDEBUG", Program.Config.WineDebugVars);
        if (Program.Config.ESyncEnabled ?? true) env.Add("WINEESYNC", "1");
        if (Program.Config.FSyncEnabled ?? false) env.Add("WINEFSYNC", "1");
        env.Add("WINEPREFIX", Path.Combine(Program.storage.Root.FullName, "wineprefix"));
        
        return new WineSettings(winepath, "", folder, url, Program.storage.Root.FullName, env);
    }

    private static WineSettings GetProton()
    {
        var proton = Path.Combine(ProtonManager.GetVersionPath(Program.Config.ProtonVersion), "proton");
        var runCmd = proton;
        var runArgs = "";
        var minRunCmd = "";

        if (Program.Config.SteamRuntime != "Disabled" && !Distro.IsFlatpak)
        {
            runCmd = Path.Combine(ProtonManager.GetRuntimePath(Program.Config.SteamRuntime), "_v2-entry-point");
            runArgs = "--verb=waitforexitandrun -- \"" + proton + "\"";
            minRunCmd = proton;
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
        env.Add("STEAM_COMPAT_DATA_PATH", Path.Combine(xlcore, "protonprefix"));
        env.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", Program.Config.SteamPath);

        var compatMounts = Environment.GetEnvironmentVariable("STEAM_COMPAT_MOUNTS") ?? "";
        var protonCompatMounts = Program.Config.GamePath + ":" + Program.Config.GameConfigPath;

        // Extra Steam compatibility mounts for discord ipc bridge
        var discordIPCPaths = "";
        if (Program.Config.SteamRuntime != "Disabled" && !Distro.IsFlatpak)
        {
            string runPath = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            for (int i = 0; i < 10; i++)
                discordIPCPaths += $"{runPath}/discord-ipc-{i}:{runPath}/app/com.discordapp.Discord/discord-ipc-{i}:{runPath}/snap.discord-cananry/discord-ipc-{i}:";
        }
        compatMounts = discordIPCPaths + protonCompatMounts + (compatMounts.Equals("") ? "" : ":" + compatMounts);
        env.Add("STEAM_COMPAT_MOUNTS", compatMounts);
        env.Add("WINEPREFIX", Path.Combine(xlcore, "protonprefix", "pfx"));
        if (!Program.Config.FSyncEnabled.Value)
        {
            if (!Program.Config.ESyncEnabled.Value)
                env.Add("PROTON_NO_ESYNC", "1");
            env.Add("PROTON_NO_FSYNC", "1");
        }

        return new WineSettings(runCmd, runArgs, "", "", xlcore, env, isProton: true, minRunCmd: minRunCmd);
    }
}




