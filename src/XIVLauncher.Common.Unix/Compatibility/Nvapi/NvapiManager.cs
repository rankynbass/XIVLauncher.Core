using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json;

using XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi;

public class NvapiManager
{
    public string DEFAULT { get; private set; }

    public string LEGACY { get; private set; }

    public Dictionary<string, IToolRelease> Version { get; private set; }

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
        Console.WriteLine("RB-nvapilist.json found!");
        NvapiList nvapiList;
        using (StreamReader file = new StreamReader(nvapiJson.OpenRead()))
        {
            nvapiList = JsonConvert.DeserializeObject<NvapiList>(file.ReadToEnd());
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
}
