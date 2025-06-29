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

    [SettingsDescription("Proton", "Use Steam sniper runtime and a patched Proton release.")]
    Proton,
}

public class WineManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IWineRelease> Version { get; private set; }

    private string wineFolder { get; }

    private string rootFolder { get; }

    public WineManager(string root)
    {
        this.rootFolder = root;
        this.wineFolder = Path.Combine(root, "compatibilitytool", "wine");

        Initialize();
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IWineRelease>();
        
        var wineDistroId = CompatUtil.GetWineIdForDistro();
        var wineStable = new WineStableRelease(wineDistroId);
        var wineBeta = new WineBetaRelease(wineDistroId);
        var wineLegacy = new WineLegacyRelease(wineDistroId);

        var wineDefault = new WineCustomRelease("Unofficial 10.10", "Rankyn's Unofficial Wine-XIV 10.10", "unofficial-wine-xiv-staging-10.10",
            $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-{wineDistroId}-10.10.tar.xz", true);

        this.DEFAULT = wineDefault.Name;
        this.LEGACY = wineLegacy.Name;

        AddVersion(wineDefault);
        AddVersion(new WineCustomRelease("Unofficial 10.10 NTSync", "Rankyn's Unofficial Wine-XIV 10.10 with NTSync", "unofficial-wine-xiv-staging-ntsync-10.10",
            "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-ntsync-10.10.tar.xz", true));
        AddVersion(new WineCustomRelease("ValveBE 9-20", "Patched Valve-Wine Bleeding Edge 9. A replacement for Wine-GE", "unofficial-wine-xiv-valvebe-9-20",
            "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-20/unofficial-wine-xiv-valvebe-9-20.tar.xz", true));
        AddVersion(new WineCustomRelease("Wine-GE-XIV 8-26", "Patched version of Wine-GE 8-26", "unofficial-wine-xiv-Proton8-26-x86_64",
            "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-26/unofficial-wine-xiv-Proton8-26-x86_64.tar.xz", false));
        AddVersion(wineStable);
        AddVersion(wineBeta);
        AddVersion(wineLegacy);
    }

    private void AddVersion(IWineRelease wine)
    {
        Version.Add(wine.Name, wine);
    }

    public string GetVersionOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DEFAULT;
        if (Version.ContainsKey(name))
            return name;
        return DEFAULT;
    }

    public IWineRelease GetWine(string? name)
    {
        return Version[GetVersionOrDefault(name)];
    }

    public void Reset()
    {
        Version.Clear();
        Initialize();
    }
}
