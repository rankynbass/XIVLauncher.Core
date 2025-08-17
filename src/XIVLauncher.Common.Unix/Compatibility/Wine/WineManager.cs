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
    [SettingsDescription("Managed Wine", "Wine setup is managed by XIVLauncher-RB - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Managed Proton", "Proton and Umu Launcher, managed by XIVLauncher-RB.")]
    Proton,

    [SettingsDescription("Custom Wine/Proton", "Point XIVLauncher-RB to a custom location containing wine OR proton binaries to run the game with.")]
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
    public string DEFAULTWINE { get; private set; }

    public string DEFAULTPROTON { get; private set; }

    public Dictionary<string, IWineRelease> WineVersion { get; private set; }

    public Dictionary<string, IWineRelease> ProtonVersion { get; private set; }

    public IToolRelease Runtime { get; private set; }

    public bool IsListUpdated { get; private set; } = false;

    private const string WINELIST_URL = "https://raw.githubusercontent.com/rankynbass/XIV-compatibilitytools/refs/heads/main/RB-runnerlist.json";

    private const string JSON_NAME = "RB-runnerlist.json";
    
    private const string UMULAUNCHER_URL = "https://github.com/Open-Wine-Components/umu-launcher/releases/download/1.2.9/umu-launcher-1.2.9-zipapp.tar";

    private WineReleaseDistro wineDistroId { get; }

    private string wineFolder { get; }

    private string umuFolder { get; }

    private string umuLauncherUrl { get; set; }

    private string rootFolder { get; }

    private string commonFolder { get; }

    private string compatFolder { get; }

    private FileInfo wineJson { get; set; }

    public DirectoryInfo SteamFolder { get; }

    public WineManager(string root)
    {
        this.rootFolder = root;
        this.wineJson = new FileInfo(Path.Combine(rootFolder, "RB-runnerlist.json"));
    
        // Wine
        this.wineFolder = Path.Combine(root, "compatibilitytool", "wine");
        if (!Directory.Exists(wineFolder))
            Directory.CreateDirectory(wineFolder);
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

        Load();
    }

    public void SetUmuLauncher(bool useBuiltinUmu)
    {
        var umuPath = findUmuLauncher(useBuiltinUmu);
        Runtime = umuPath is null ? new UmuLauncherRelease(Path.Combine(umuFolder, "umu-run"), this.umuLauncherUrl) : new UmuLauncherRelease(umuPath, "");
    }

    private void Load()
    {
        if (wineJson.Exists)
            InitializeJson();
        else
            InitializeDefault();

        InitializeLocalWine();
        InitializeLocalProton();
    }

    public void Reload()
    {
        this.IsListUpdated = true;
        Load();
    }

    private void InitializeDefault()
    {
        WineVersion = new Dictionary<string, IWineRelease>();
        ProtonVersion = new Dictionary<string, IWineRelease>();
        
        // Wine
        var wineStable = new WineStableRelease(wineDistroId, wineFolder);
        var wineBeta = new WineBetaRelease(wineDistroId, wineFolder);
        var wineLegacy = new WineLegacyRelease(wineDistroId, wineFolder);

        var wineDefault = new WineCustomRelease("Unofficial 10.10", "Rankyn's Unofficial Wine-XIV 10.10", "unofficial-wine-xiv-staging-10.10", wineFolder,
            $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v10.10/unofficial-wine-xiv-staging-{wineDistroId}-10.10.tar.xz", true);

        this.DEFAULTWINE = wineDefault.Name;

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

        this.DEFAULTPROTON = protonLatest.Name;

        AddVersion(protonLatest);
        AddVersion(protonLatestNtsync);
        AddVersion(protonStable);
        AddVersion(protonStableNtsync);
        AddVersion(protonLegacy);

        this.umuLauncherUrl = UMULAUNCHER_URL;
    }

    private WineList? ReadJsonFile(FileInfo jsonFile)
    {
        WineList wineList;
        using (var file = new StreamReader(jsonFile.OpenRead()))
        {
            try
            {
                wineList = JsonConvert.DeserializeObject<WineList>(file.ReadToEnd());
                if (string.IsNullOrEmpty(wineList.UmuLauncherUrl) || string.IsNullOrEmpty(wineList.DefaultWine) || string.IsNullOrEmpty(wineList.DefaultProton))
                    throw new JsonSerializationException("JSON file is invalid: missing entries");
                if (wineList.WineVersions.Count == 0)
                    throw new JsonSerializationException("JSON file is invalid: wine list empty");
                if (wineList.ProtonVersions.Count == 0)
                    throw new JsonSerializationException("JSON file is invalid: proton list empty");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{jsonFile.FullName} is invalid.");
                wineList = null;
            }                
        }
        return wineList;
    }

    private void InitializeJson()
    {
        WineVersion = new Dictionary<string, IWineRelease>();
        ProtonVersion = new Dictionary<string, IWineRelease>();
        string umuLauncherUrl;
        DateTime releaseDate;
        WineList wineList = ReadJsonFile(wineJson);
        if (wineList is null)
        {
            InitializeDefault();
            IsListUpdated = true;
            return;
        }
        
        foreach (var wineRelease in wineList.WineVersions)
        {
            AddVersion(new WineCustomRelease(wineRelease.Label, wineRelease.Description, wineRelease.Name, this.wineFolder, wineRelease.DownloadUrl.Replace("{wineDistroId}", wineDistroId.ToString()), wineRelease.Lsteamclient, wineRelease.Checksums));
        }
        foreach (var protonRelease in wineList.ProtonVersions)
        {
            AddVersion(new ProtonCustomRelease(protonRelease.Label, protonRelease.Description, protonRelease.Name, this.compatFolder, protonRelease.DownloadUrl, protonRelease.Checksums[0]));
        }
        
        this.DEFAULTWINE = wineList.DefaultWine;
        this.DEFAULTPROTON = wineList.DefaultProton;
        this.umuLauncherUrl = wineList.UmuLauncherUrl;
    }

    private void InitializeLocalWine()
    {
        var wineToolDir = new DirectoryInfo(wineFolder);
        if (!wineToolDir.Exists)
            return;
        foreach (var wineDir in wineToolDir.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (WineVersion.ContainsKey(wineDir.Name))
                continue;
            if (File.Exists(Path.Combine(wineDir.FullName, "bin", "wine64")) ||
                File.Exists(Path.Combine(wineDir.FullName, "bin", "wine")))
            {
                AddVersion(new WineCustomRelease(wineDir.Name, $"Custom wine in {wineFolder}", wineDir.Name, wineFolder, "", WineSettings.HasLsteamclient(Path.Combine(wineFolder, wineDir.Name))));
            }
        }
    }

    private void InitializeLocalProton()
    {
        var compatibilitytoolsd = new DirectoryInfo(compatFolder);
        foreach (var protonDir in compatibilitytoolsd.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (ProtonVersion.ContainsKey(protonDir.Name))
                continue;
            if (File.Exists(Path.Combine(protonDir.FullName, "proton")))
            {
                string name;
                if (protonDir.Name.Contains("GE-"))
                    name = "GE Proton";
                else if (protonDir.Name.Contains("XIV-"))
                    name = "XIV-Proton";
                else if (protonDir.Name.ToLowerInvariant().Contains("cachyos"))
                    name = "CachyOS Proton";
                else
                    name = "Proton";
                AddVersion(new ProtonCustomRelease(protonDir.Name, $"{name} in {compatFolder}", protonDir.Name, compatFolder, ""));
            }
        }

        var steamappsCommon = new DirectoryInfo(commonFolder);
        foreach (var protonDir in steamappsCommon.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (ProtonVersion.ContainsKey(protonDir.Name))
                continue;
            if (File.Exists(Path.Combine(protonDir.FullName, "proton")))
            {
                string name;
                if (protonDir.Name.Contains("GE-"))
                    name = "GE Proton";
                else if (protonDir.Name.Contains("XIV-"))
                    name = "XIV-Proton";
                else if (protonDir.Name.ToLowerInvariant().Contains("cachyos"))
                    name = "CachyOS Proton";
                else
                    name = "Proton";
                AddVersion(new ProtonCustomRelease(protonDir.Name, $"{name} in {commonFolder}", protonDir.Name, commonFolder, ""));
            }
        }
    }

    private void AddVersion(IWineRelease wine)
    {
        if (wine.IsProton)
            ProtonVersion.Add(wine.Name, wine);
        else
            WineVersion.Add(wine.Name, wine);
    }

    public string GetWineVersionOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DEFAULTWINE;
        if (WineVersion.ContainsKey(name))
            return name;
        return DEFAULTWINE;
    }

    public string GetProtonVersionOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DEFAULTPROTON;
        if (ProtonVersion.ContainsKey(name))
            return name;
        return DEFAULTPROTON;
    }

    public IWineRelease GetWine(string? name)
    {
        return WineVersion[GetWineVersionOrDefault(name)];
    }

    public IWineRelease GetProton(string? name)
    {
        return ProtonVersion[GetProtonVersionOrDefault(name)];
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
        client.Timeout = TimeSpan.FromSeconds(5);
        var tempPath = PlatformHelpers.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(WINELIST_URL).ConfigureAwait(false));

        if (ReadJsonFile(new FileInfo(tempPath)) is null)
            return;

        if (!wineJson.Exists)
        {
            File.Move(tempPath, wineJson.FullName);
            wineJson = new FileInfo(wineJson.FullName);
            Reload();
            return;
        }

        using var sha512 = SHA512.Create();
        using var tempPathStream = File.OpenRead(tempPath);
        using var wineListStream = wineJson.OpenRead();
        var tempPathHash = Convert.ToHexString(sha512.ComputeHash(tempPathStream)).ToLowerInvariant();
        var wineListHash = Convert.ToHexString(sha512.ComputeHash(wineListStream)).ToLowerInvariant();
        if (tempPathHash != wineListHash)
        {
            wineJson.Delete();
            File.Move(tempPath, wineJson.FullName);
            Reload();
        }        
    }

    public void DoneUpdatingWineList()
    {
        IsListUpdated = false;
    }
}
