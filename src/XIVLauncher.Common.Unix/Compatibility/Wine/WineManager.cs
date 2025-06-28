using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public enum RBWineStartupType
{
    [SettingsDescription("Managed by XIVLauncher-RB", "Wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Custom", "Point XIVLauncher-RB to a custom location containing wine binaries to run the game with.")]
    Custom,

    // [SettingsDescription("Proton", "Use Steam sniper runtime and a patched Proton release.")]
    // Proton,
}

public class WineManager
{
    public Dictionary<string, IWineRelease> Version { get; private set; }

    private string wineFolder { get; }

    private string rootFolder { get; }

    public WineManager(string root)
    {
        this.rootFolder = root;
        this.wineFolder = Path.Combine(root, "compatibilitytool", "wine");

        Initialize();
    }

    public void Initialize()
    {
        Version = new Dictionary<string, IWineRelease>();
        
        var wineDistroId = CompatUtil.GetWineIdForDistro();
        var wineStable = new WineStableRelease(wineDistroId);
        var wineBeta = new WineBetaRelease(wineDistroId);
        var wineLegacy = new WineLegacyRelease(wineDistroId);

        Version.Add(wineStable.Name, wineStable);
        Version.Add(wineBeta.Name, wineBeta);
        Version.Add(wineLegacy.Name, wineLegacy);
    }

    public void Reset()
    {
        Version.Clear();
        Initialize();
    }
}
