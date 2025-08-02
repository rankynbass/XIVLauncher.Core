using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json;

using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Dxvk;

public class DxvkManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IToolRelease> Version { get; private set; }

    private string dxvkFolder { get; }

    private string rootFolder { get; }

    public DxvkManager(string root)
    {
        this.rootFolder = root;
        this.dxvkFolder = Path.Combine(root, "compatibilitytool", "dxvk");

        var dxvkJson = new FileInfo(Path.Combine(rootFolder, "RB-dxvklist.json"));
        if (dxvkJson.Exists)
            InitializeJson(dxvkJson);
        else
            Initialize();
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IToolRelease>();
        
        var dxvkStable = new DxvkStableRelease();
        var dxvkStableAsync = new DxvkStableAsyncRelease();
        var dxvkStable22 = new DxvkStable22Release();
        var dxvkLegacy = new DxvkLegacyRelease();

        this.DEFAULT = dxvkStable.Name;
        this.LEGACY = dxvkLegacy.Name;

        AddVersion(dxvkStable);
        AddVersion(dxvkStableAsync);
        AddVersion(dxvkStable22);
        AddVersion(dxvkLegacy);
        AddVersion(new DxvkCustomRelease("Disabled", "Use WineD3D instead", "DISABLED", ""));
    }

    private void InitializeJson(FileInfo dxvkJson)
    {
        Version = new Dictionary<string, IToolRelease>();
        DateTime releaseDate;
        Console.WriteLine("RB-dxvklist.json found!");
        DxvkList dxvkList;
        using (StreamReader file = new StreamReader(dxvkJson.OpenRead()))
        {
            dxvkList = JsonConvert.DeserializeObject<DxvkList>(file.ReadToEnd());
        }
        foreach (var dxvkRelease in dxvkList.DxvkVersions)
        {
            AddVersion(new DxvkCustomRelease(dxvkRelease.Label, dxvkRelease.Description, dxvkRelease.Name, dxvkRelease.DownloadUrl, dxvkRelease.Checksum));
        }
        AddVersion(new DxvkCustomRelease("Disabled", "Use WineD3D instead", "DISABLED", ""));

        this.LEGACY = dxvkList.Legacy;
        this.DEFAULT = dxvkList.Latest;
    }

    private void AddVersion(IToolRelease dxvk)
    {
        Version.Add(dxvk.Name, dxvk);
    }

    public string GetVersionOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DEFAULT;
        if (Version.ContainsKey(name))
            return name;
        return DEFAULT;
    }

    public IToolRelease GetDxvk(string? name)
    {
        return Version[GetVersionOrDefault(name)];
    }

    public void Reset()
    {
        Version.Clear();
        Initialize();
    }
}
