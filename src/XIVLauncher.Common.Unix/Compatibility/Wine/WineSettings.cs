using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public enum WineStartupType
{
    [SettingsDescription("Managed by XIVLauncher", "Wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public enum WineManagedVersion
{
    [SettingsDescription("Stable", "Based on Wine 10.8 - recommended for most users.")]
    Stable,

    [SettingsDescription("Beta", "Testing ground for the newest wine changes. Based on Wine 10.8 with lsteamclient patches.")]
    Beta,

    [SettingsDescription("Legacy", "Based on Wine 8.5 - use for compatibility with some plugins.")]
    Legacy,
}

public class WineSettings
{
    public WineStartupType StartupType { get; private set; }
    public IWineRelease WineRelease { get; private set; }

    public string CustomBinPath { get; private set; }
    public string EsyncOn { get; private set; }
    public string FsyncOn { get; private set; }
    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }
    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(WineStartupType startupType, WineManagedVersion managedWine, string customBinPath, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool esyncOn, bool fsyncOn)
    {
        this.StartupType = startupType;

        var wineDistroId = CompatUtil.GetWineIdForDistro();
        switch (managedWine)
        {
            case WineManagedVersion.Stable:
                this.WineRelease = new WineCustomRelease("wine-xiv-staging-fsync-git-10.8.r0.g47f77594-nolsc", $"https://github.com/goatcorp/wine-xiv-git/releases/download/10.8.r0.g47f77594/wine-xiv-staging-fsync-git-{wineDistroId}-10.8.r0.g47f77594-nolsc.tar.xz",
                                    [
                                        "e7803fff77cec837f604eef15af8434b4d74acd0e3adf1885049b31143bdd6b69f03f56b14f078e501f42576b3b4434deca547294b2ded0c471720ef7e412367", // wine-xiv-staging-fsync-git-arch-10.8.r0.g47f77594-nolsc.tar.xz
                                        "7475788ba4cd448743fa44acba475eac796c9fe1ec8a2b37e0fdb7123cf3feac0c97f0a4e43ea023bf1e70853e7916a5a27e835fc5f651ac5c08040251bc4522",  // wine-xiv-staging-fsync-git-fedora-10.8.r0.g47f77594-nolsc.tar.xz
                                        "9d06e403b0b879a7b1f6394d69a6d23ee929c27f1f7a3abbf0f34fab3cbaff0b8154849d406f3ed15ee62ec0444379173070da208607fadabbf65186ed0cbf95" // wine-xiv-staging-fsync-git-ubuntu-10.8.r0.g47f77594-nolsc.tar.xz
                                    ], false, wineDistroId);
                break;
            case WineManagedVersion.Beta:
                this.WineRelease = new WineBetaRelease(wineDistroId);
                break;
            case WineManagedVersion.Legacy:
                this.WineRelease = new WineLegacyRelease(wineDistroId);
                break;
            default:
                throw new ArgumentOutOfRangeException(managedWine.ToString());
        }
        this.CustomBinPath = customBinPath;
        this.EsyncOn = esyncOn ? "1" : "0";
        this.FsyncOn = fsyncOn ? "1" : "0";
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
