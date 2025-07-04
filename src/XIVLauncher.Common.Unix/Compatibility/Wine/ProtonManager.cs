using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public class ProtonManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IWineRelease> Version { get; private set; }

    public IToolRelease Runtime { get; }

    private string rootFolder { get; }

    private string commonFolder { get; }

    private string compatFolder { get; }

    public DirectoryInfo SteamFolder { get; }

    public ProtonManager(string root)
    {
        this.rootFolder = root;
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
        
        var protonStable = new ProtonStableRelease(compatFolder);
        var protonStableNtsync = new ProtonStableNtsyncRelease(compatFolder);
        var protonLatest = new ProtonLatestRelease(compatFolder);
        var protonLatestNtsync = new ProtonLatestNtsyncRelease(compatFolder);
        var protonLegacy = new ProtonLegacyRelease(compatFolder);

        this.DEFAULT = protonLatest.Name;
        this.LEGACY = protonLegacy.Name;

        AddVersion(protonLatest);
        AddVersion(protonLatestNtsync);
        AddVersion(protonStable);
        AddVersion(protonStableNtsync);
        AddVersion(protonLegacy);
    }

    private void AddVersion(IWineRelease proton)
    {
        Version.Add(proton.Name, proton);
    }

    public string GetVersionOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DEFAULT;
        if (Version.ContainsKey(name))
            return name;
        return DEFAULT;
    }

    public IToolRelease GetProton(string? name)
    {
        return Version[GetVersionOrDefault(name)];
    }

    public void Reset()
    {
        Version.Clear();
        Initialize();
    }
}
