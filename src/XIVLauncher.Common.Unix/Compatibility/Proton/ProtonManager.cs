using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

using XIVLauncher.Common.Unix.Compatibility.Proton.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Proton;

public class ProtonManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IToolRelease> Version { get; private set; }

    private string rootFolder { get; }

    public string commonFolder { get; }

    public string compatFolder { get; }

    public ProtonManager(string root)
    {
        this.rootFolder = root;
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var steamfolder1 = Path.Combine(home, ".steam", "steam", "steamapps", "common");
        var steamfolder2 = Path.Combine(home, ".local", "share", "Steam", "steamapps", "common");
        if (Directory.Exists(steamfolder1))
        {
            this.commonFolder = steamfolder1;
            this.compatFolder = Path.Combine(home, ".steam", "steam", "compatibilitytools.d");
        }
        else
        {
            this.commonFolder = steamfolder2;
            this.compatFolder = Path.Combine(home, ".local", "share", "Steam", "compatibilitytools.d");
        }
        Initialize();
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IToolRelease>();
        
        var protonStable = new ProtonStableRelease();
        var protonStableNtsync = new ProtonStableNtsyncRelease();
        var protonLatest = new ProtonLatestRelease();
        var protonLatestNtsync = new ProtonLatestNtsyncRelease();
        var protonLegacy = new ProtonLegacyRelease();

        this.DEFAULT = protonLatest.Name;
        this.LEGACY = protonLegacy.Name;

        AddVersion(protonLatest);
        AddVersion(protonLatestNtsync);
        AddVersion(protonStable);
        AddVersion(protonStableNtsync);
        AddVersion(protonLegacy);
    }

    private void AddVersion(IToolRelease proton)
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

    public string GetProtonPath(IToolRelease name)
    {
        if (File.Exists(Path.Combine(commonFolder, name.Name, "proton")))
            return Path.Combine(commonFolder, name.Name);
        if (File.Exists(Path.Combine(compatFolder, name.Name, "proton")))
            return Path.Combine(commonFolder, name.Name);
        return string.Empty;
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
