using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Serilog;

using XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi;

public class NvapiManager
{
    public string DEFAULT { get; private set; }

    public Dictionary<string, IToolRelease> Version { get; private set; }

    public bool IsListUpdated { get; set; } = false;

    private const string NVAPILIST_URL = "https://raw.githubusercontent.com/rankynbass/XIV-compatibilitytools/refs/heads/main/RB-nvapilist.json";

    private const string JSON_NAME = "RB-nvapilist.json";

    private string nvapiFolder { get; }

    private string rootFolder { get; }

    private FileInfo nvapiJson { get; }

    public NvapiManager(string root)
    {
        this.rootFolder = root;
        this.nvapiFolder = Path.Combine(root, "compatibilitytool", "nvapi");
        if (!Directory.Exists(nvapiFolder))
            Directory.CreateDirectory(nvapiFolder);

        this.nvapiJson = new FileInfo(Path.Combine(rootFolder, JSON_NAME));
        Load();
    }

    private void Load()
    {
        if (nvapiJson.Exists)
            InitializeJson();
        else
            InitializeDefault();
    }

    public void Reload()
    {
        this.IsListUpdated = true;
        Load();
    }

    private void InitializeDefault()
    {
        Version = new Dictionary<string, IToolRelease>();
        
        var nvapiStable = new NvapiStableRelease();

        this.DEFAULT = nvapiStable.Name;

        AddVersion(nvapiStable);
        AddVersion(new NvapiCustomRelease("Disabled", "Do not use Nvapi", "DISABLED", ""));
    }

    private NvapiList? ReadJsonFile(FileInfo jsonFile)
    {
        NvapiList nvapiList;
        using (var file = new StreamReader(jsonFile.OpenRead()))
        {
            try
            {
                nvapiList = JsonConvert.DeserializeObject<NvapiList>(file.ReadToEnd());
                if (string.IsNullOrEmpty(nvapiList.Latest))
                    throw new JsonSerializationException("JSON file is invalid: nvapi list is missing entries");
                if (nvapiList.NvapiVersions.Count == 0)
                    throw new JsonSerializationException("JSON file is invalid: nvapi version list empty");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{jsonFile.FullName} is invalid.");
                nvapiList = null;
            }                
        }
        return nvapiList;
    }

    private void InitializeJson()
    {
        Version = new Dictionary<string, IToolRelease>();
        DateTime releaseDate;
        NvapiList nvapiList = ReadJsonFile(nvapiJson);
        if (nvapiList is null)
        {
            InitializeDefault();
            IsListUpdated = true;
            return;
        }

        foreach (var nvapiRelease in nvapiList.NvapiVersions)
        {
            AddVersion(new NvapiCustomRelease(nvapiRelease.Label, nvapiRelease.Description, nvapiRelease.Name, nvapiRelease.DownloadUrl, nvapiRelease.Checksum));
        }
        AddVersion(new NvapiCustomRelease("Disabled", "Do not use Nvapi", "DISABLED", ""));

        this.DEFAULT = nvapiList.Latest;
    }

    private void InitializeLocalNvapi()
    {
        var nvapiToolDir = new DirectoryInfo(nvapiFolder);
        foreach (var nvapiDir in nvapiToolDir.EnumerateDirectories().OrderBy(x => x.Name))
        {
            if (Version.ContainsKey(nvapiDir.Name))
                continue;
            if (Directory.Exists(Path.Combine(nvapiDir.FullName, "x64")))
                AddVersion(new NvapiCustomRelease(nvapiDir.Name, $"Custom nvapi in {nvapiFolder}", nvapiDir.Name, ""));
        }
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

    public async Task DownloadNvapiList()
    {
        // Uncomment for testing
        // await Task.Delay(5000);
        
        using var client = HappyEyeballsHttp.CreateHttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(NVAPILIST_URL).ConfigureAwait(false));

        if (ReadJsonFile(new FileInfo(tempPath)) is null)
            return;

        if (!nvapiJson.Exists)
        {
            File.Move(tempPath, nvapiJson.FullName);
            Reload();
            return;
        }

        using var sha512 = SHA512.Create();
        using var tempPathStream = File.OpenRead(tempPath);
        using var nvapiListStream = nvapiJson.OpenRead();
        var tempPathHash = Convert.ToHexString(sha512.ComputeHash(tempPathStream)).ToLowerInvariant();
        var nvapiListHash = Convert.ToHexString(sha512.ComputeHash(nvapiListStream)).ToLowerInvariant();
        if (tempPathHash != nvapiListHash)
        {
            nvapiJson.Delete();
            File.Move(tempPath, nvapiJson.FullName);
            Reload();
        }
    }

    public void DoneUpdatingNvapiList()
    {
        IsListUpdated = false;
    }
}
