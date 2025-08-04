using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

using Newtonsoft.Json;
using Serilog;

using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public enum RBWineStartupType
{
    [SettingsDescription("Managed by XIVLauncher-RB", "Wine/Proton setup is managed by XIVLauncher-RB - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Custom Wine/Proton", "Point XIVLauncher-RB to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public enum RBUmuLauncherType
{
    [SettingsDescription("System", "Use system Umu Launcher if available. This will fall back to builtin Umu if it is not installed.")]
    System,

    [SettingsDescription("Built-in", "Use the built-in Umu Launcher, even if Umu is installed on the system.")]
    Builtin,

    [SettingsDescription("Disabled", "Don't use Umu-launcher with proton")]
    Disabled,
}
public class WineManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IWineRelease> Version { get; private set; }

    public IToolRelease Runtime { get; private set; }

    public bool IsListUpdated { get; private set; } = false;

    private const string WINELIST_URL = "https://raw.githubusercontent.com/rankynbass/XIV-compatibilitytools/refs/heads/main/RB-winelist.json";

    private WineReleaseDistro wineDistroId { get; }

    private string wineFolder { get; }

    private string umuFolder { get; }

    private string umuLauncherUrl { get; set; }

    private string rootFolder { get; }

    private string commonFolder { get; }

    private string compatFolder { get; }

    public DirectoryInfo SteamFolder { get; }

    public WineManager(string root)
    {
        this.rootFolder = root;
    
        // Wine
        this.wineFolder = Path.Combine(root, "compatibilitytool", "wine");
        this.wineDistroId = CompatUtil.GetWineIdForDistro();

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

        // Umu Launcher
        this.umuFolder = Path.Combine(root, "compatibilitytool", "umu");

        var wineJson = new FileInfo(Path.Combine(rootFolder, "RB-winelist.json"));
        if (wineJson.Exists)
            InitializeJson(wineJson);
        else
            Initialize();
    }

    public void SetUmuLauncher(bool useBuiltinUmu)
    {
        var umuPath = findUmuLauncher(useBuiltinUmu);
        Runtime = umuPath is null ? new UmuLauncherRelease(Path.Combine(umuFolder, "umu-run"), this.umuLauncherUrl) : new UmuLauncherRelease(umuPath, "");
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IWineRelease>();
        
        // Wine
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
        
        InitializeLocalWine();
        InitializeLocalProton();
    }

    private void InitializeJson(FileInfo wineJson)
    {
        Version = new Dictionary<string, IWineRelease>();
        string umuLauncherUrl;
        DateTime releaseDate;
        WineList wineList;
        using (StreamReader file = new StreamReader(wineJson.OpenRead()))
        {
            wineList = JsonConvert.DeserializeObject<WineList>(file.ReadToEnd());
        }
        foreach (var wineRelease in wineList.WineVersions)
        {
            if (wineRelease.IsProton)
            {
                try
                {
                    AddVersion(new ProtonCustomRelease(wineRelease.Label, wineRelease.Description, wineRelease.Name, this.compatFolder, wineRelease.DownloadUrl, wineRelease.Checksums[0]));
                }
                catch
                {
                    Initialize();
                    return;
                }
            }
            else
            {
                AddVersion(new WineCustomRelease(wineRelease.Label, wineRelease.Description, wineRelease.Name, this.wineFolder, wineRelease.DownloadUrl.Replace("{wineDistroId}", wineDistroId.ToString()), wineRelease.Lsteamclient, wineRelease.Checksums));
            }
        }
        this.LEGACY = wineList.Legacy;
        this.DEFAULT = wineList.Latest;
        this.umuLauncherUrl = wineList.UmuLauncherUrl;
        InitializeLocalWine();
        InitializeLocalProton();
    }

    private void InitializeLocalWine()
    {
        var wineToolDir = new DirectoryInfo(wineFolder);
        foreach (var wineDir in wineToolDir.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (File.Exists(Path.Combine(wineDir.FullName, "bin", "wine64")) ||
                File.Exists(Path.Combine(wineDir.FullName, "bin", "wine")))
            {
                if (Version.ContainsKey(wineDir.Name))
                {
                    continue;
                }
                AddVersion(new WineCustomRelease(wineDir.Name, $"Custom wine at {wineFolder}", wineDir.Name, wineFolder, "", WineSettings.HasLsteamclient(Path.Combine(wineFolder, wineDir.Name))));
            }
        }
    }

    private void InitializeLocalProton()
    {

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

    private string? findUmuLauncher(bool useBuiltinUmu)
    {
        if (useBuiltinUmu)
            return null;
        var pathArray = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':');
        foreach (string test in pathArray)
        {
            if (string.IsNullOrEmpty(test.Trim()))
                continue;
            string umu = Path.Combine(test.Trim(), "umu-run");
            if (File.Exists(umu))
                return Path.GetFullPath(umu);
        }
        return null;
    }

    public async Task DownloadWineList()
    {
        // Uncomment for testing
        // await Task.Delay(5000);

        using var client = HappyEyeballsHttp.CreateHttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();
        var wineList = Path.Combine(rootFolder, "RB-winelist.json");

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(WINELIST_URL).ConfigureAwait(false));

        if (!File.Exists(wineList))
        {
            File.Move(tempPath, wineList);
            IsListUpdated = true;
            InitializeJson(new FileInfo(wineList));
            return;
        }

        using var sha512 = SHA512.Create();
        using var tempPathStream = File.OpenRead(tempPath);
        using var wineListStream = File.OpenRead(wineList);
        var tempPathHash = Convert.ToHexString(sha512.ComputeHash(tempPathStream)).ToLowerInvariant();
        var wineListHash = Convert.ToHexString(sha512.ComputeHash(wineListStream)).ToLowerInvariant();
        if (tempPathHash != wineListHash)
        {
            File.Delete(wineList);
            File.Move(tempPath, wineList);
            IsListUpdated = true;
            InitializeJson(new FileInfo(wineList));
        }        
    }

    public void DoneUpdatingWineList()
    {
        IsListUpdated = false;
    }
}
