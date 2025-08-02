using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;

using XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi;

public class NvapiManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IToolRelease> Version { get; private set; }

    public bool IsListUpdated { get; set; } = false;

    private const string NVAPILIST_URL = "https://raw.githubusercontent.com/rankynbass/XIV-compatibilitytools/refs/heads/main/RB-nvapilist.json";

    private string nvapiFolder { get; }

    private string rootFolder { get; }

    public NvapiManager(string root)
    {
        this.rootFolder = root;
        this.nvapiFolder = Path.Combine(root, "compatibilitytool", "nvapi");

        var nvapiJson = new FileInfo(Path.Combine(rootFolder, "RB-nvapilist.json"));
        if (nvapiJson.Exists)
            InitializeJson(nvapiJson);
        else
            Initialize();
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IToolRelease>();
        
        var nvapiStable = new NvapiStableRelease();

        this.DEFAULT = nvapiStable.Name;

        AddVersion(nvapiStable);
        AddVersion(new NvapiCustomRelease("Disabled", "Do not use Nvapi", "DISABLED", ""));
    }

    private void InitializeJson(FileInfo nvapiJson)
    {
        Version = new Dictionary<string, IToolRelease>();
        DateTime releaseDate;
        NvapiList nvapiList;
        using (StreamReader file = new StreamReader(nvapiJson.OpenRead()))
        {
            try
            {
                nvapiList = JsonConvert.DeserializeObject<NvapiList>(file.ReadToEnd());
            }
            catch
            {
                Initialize();
                return;
            }
        }
        foreach (var nvapiRelease in nvapiList.NvapiVersions)
        {
            AddVersion(new NvapiCustomRelease(nvapiRelease.Label, nvapiRelease.Description, nvapiRelease.Name, nvapiRelease.DownloadUrl, nvapiRelease.Checksum));
        }
        AddVersion(new NvapiCustomRelease("Disabled", "Do not use Nvapi", "DISABLED", ""));

        this.LEGACY = nvapiList.Legacy;
        this.DEFAULT = nvapiList.Latest;
    }

    private void AddVersion(IToolRelease nvapi)
    {
        Version.Add(nvapi.Name, nvapi);
    }

    public string GetVersionOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DEFAULT;
        if (Version.ContainsKey(name))
            return name;
        return DEFAULT;
    }

    public IToolRelease GetNvapi(string? name)
    {
        return Version[GetVersionOrDefault(name)];
    }

    public void Reset()
    {
        Version.Clear();
        Initialize();
    }

    public async Task DownloadNvapiList()
    {
        // Uncomment for testing
        // await Task.Delay(5000);
        
        using var client = HappyEyeballsHttp.CreateHttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();
        var nvapiList = Path.Combine(rootFolder, "RB-nvapilist.json");

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(NVAPILIST_URL).ConfigureAwait(false));

        if (!File.Exists(nvapiList))
        {
            File.Move(tempPath, nvapiList);
            IsListUpdated = true;
            InitializeJson(new FileInfo(nvapiList));
            return;
        }

        using var sha512 = SHA512.Create();
        using var tempPathStream = File.OpenRead(tempPath);
        using var nvapiListStream = File.OpenRead(nvapiList);
        var tempPathHash = Convert.ToHexString(sha512.ComputeHash(tempPathStream)).ToLowerInvariant();
        var nvapiListHash = Convert.ToHexString(sha512.ComputeHash(nvapiListStream)).ToLowerInvariant();
        if (tempPathHash != nvapiListHash)
        {
            File.Delete(nvapiList);
            File.Move(tempPath, nvapiList);
            IsListUpdated = true;
            InitializeJson(new FileInfo(nvapiList));
        }
       
    }

    public void DoneUpdatingNvapiList()
    {
        IsListUpdated = false;
    }
}
