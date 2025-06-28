using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

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

        Initialize();
    }

    private void Initialize()
    {
        Version = new Dictionary<string, IToolRelease>();
        
        var dxvkStable = new DxvkStableRelease();
        var dxvkStableAsync = new DxvkStableAsyncRelease();
        var dxvkLegacy = new DxvkLegacyRelease();

        this.DEFAULT = dxvkStable.Name;
        this.LEGACY = dxvkLegacy.Name;

        AddVersion(dxvkStable);
        AddVersion(dxvkStableAsync);
        AddVersion(dxvkLegacy);
        AddVersion(new DxvkCustomRelease("Disabled", "Use WineD3D instead", "DISABLED", ""));
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
