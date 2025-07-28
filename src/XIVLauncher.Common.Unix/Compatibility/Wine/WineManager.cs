using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public enum RBWineStartupType
{
    [SettingsDescription("Managed by XIVLauncher-RB", "Wine/Proton setup is managed by XIVLauncher-RB - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Custom Wine/Proton", "Point XIVLauncher-RB to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public class WineManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IWineRelease> Version { get; private set; }

    public IToolRelease Runtime { get; }

    private string wineFolder { get; }

    private string rootFolder { get; }

    private string commonFolder { get; }

    private string compatFolder { get; }

    public DirectoryInfo SteamFolder { get; }

    public WineManager(string root)
    {
        this.rootFolder = root;
    
        // Wine
        this.wineFolder = Path.Combine(root, "compatibilitytool", "wine");

        // Proton
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var steamfolder1 = Path.Combine(home, ".steam", "steam", "steamapps", "common");
        var steamfolder2 = Path.Combine(home, ".local", "share", "Steam", "steamapps", "common");
        if (Directory.Exists(steamfolder1))
        {
            this.SteamFolder = new DirectoryInfo(Path.Combine(home, ".steam", "steam"));
            this.commonFolder = steamfolder1;
            this.compatFolder = Path.Combine(home, ".steam", "steam", "compatibilitytools.d");
        }
        else
        {
            this.SteamFolder = new DirectoryInfo(Path.Combine(home, ".local", "share", "Steam"));
            this.commonFolder = steamfolder2;
            this.compatFolder = Path.Combine(home, ".local", "share", "Steam", "compatibilitytools.d");
        }
        if (!Directory.Exists(commonFolder))
            Directory.CreateDirectory(commonFolder);
        if (!Directory.Exists(compatFolder))
            Directory.CreateDirectory(compatFolder);
        this.Runtime = new SteamRuntimeRelease(this.commonFolder);

        Initialize();
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IWineRelease>();
        
        // Wine
        var wineDistroId = CompatUtil.GetWineIdForDistro();
        var wineStable = new WineStableRelease(wineDistroId, wineFolder);
        var wineBeta = new WineBetaRelease(wineDistroId, wineFolder);
        var wineLegacy = new WineLegacyRelease(wineDistroId, wineFolder);

        var wineDefault = new WineCustomRelease("Unofficial 10.10", "Rankyn's Unofficial Wine-XIV 10.10", "unofficial-wine-xiv-staging-10.10", wineFolder,
            $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-{wineDistroId}-10.10.tar.xz", true);

        this.DEFAULT = wineDefault.Name;
        this.LEGACY = wineLegacy.Name;

        AddVersion(wineDefault);
        AddVersion(new WineCustomRelease("Unofficial 10.10 NTSync", "Rankyn's Unofficial Wine-XIV 10.10 with NTSync", "unofficial-wine-xiv-staging-ntsync-10.10", wineFolder,
            "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-ntsync-10.10.tar.xz", true));
        AddVersion(new WineCustomRelease("ValveBE 9-20", "Patched Valve-Wine Bleeding Edge 9. A replacement for Wine-GE", "unofficial-wine-xiv-valvebe-9-20", wineFolder,
            "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/valvebe-9-20/unofficial-wine-xiv-valvebe-9-20.tar.xz", true));
        AddVersion(new WineCustomRelease("Wine-GE-XIV 8-26", "Patched version of Wine-GE 8-26", "unofficial-wine-xiv-Proton8-26-x86_64", wineFolder,
            "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-26/unofficial-wine-xiv-Proton8-26-x86_64.tar.xz", false));
        AddVersion(wineStable);
        AddVersion(wineBeta);
        AddVersion(wineLegacy);

        // Proton
        var protonStable = new ProtonStableRelease(compatFolder);
        var protonStableNtsync = new ProtonStableNtsyncRelease(compatFolder);
        var protonLatest = new ProtonLatestRelease(compatFolder);
        var protonLatestNtsync = new ProtonLatestNtsyncRelease(compatFolder);
        var protonLegacy = new ProtonLegacyRelease(compatFolder);

        AddVersion(protonLatest);
        AddVersion(protonLatestNtsync);
        AddVersion(protonStable);
        AddVersion(protonStableNtsync);
        AddVersion(protonLegacy);
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

    public bool IsProton(string? name)
    {
        return Version[GetVersionOrDefault(name)].IsProton;
    }

    public void Reset()
    {
        Version.Clear();
        Initialize();
    }
}
