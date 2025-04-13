using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;
using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;


namespace XIVLauncher.Common.Unix.Compatibility;

public static class CompatToolbox
{
    public static Dictionary<string, Dictionary<string, CompatToolRelease>> Tools { get; private set; }

    private static DirectoryInfo Storage;

    public static bool Initialized { get; private set; } = false;

    private static WineReleaseDistro wineDistroId;

    public static void Initialize(DirectoryInfo storageFolder)
    {
        Storage = storageFolder;
        wineDistroId = CompatUtil.GetWineIdForDistro();
        var jsonFile = Path.Combine(storageFolder.FullName, "compattools.json");
        var defaultWineStable = ConvertToCompatToolRelease(new WineStableRelease(wineDistroId));
        var defaultWineLegacy = ConvertToCompatToolRelease(new WineLegacyRelease(wineDistroId));
        var defaultDxvkStable = ConvertToCompatToolRelease(new DxvkStableRelease());
        var defaultDxvkLegacy = ConvertToCompatToolRelease(new DxvkLegacyRelease());
        var defaultDxvkDisabled = ConvertToCompatToolRelease(new DxvkDisabledRelease());
        
        Console.WriteLine("WineStableRelease.Name = " + defaultWineStable.Name);

        if (!File.Exists(jsonFile))
        {
            Tools = new Dictionary<string, Dictionary<string, CompatToolRelease>>()
            {
                {
                    "Wine", new Dictionary<string, CompatToolRelease>()
                    { 
                        { defaultWineStable.Name.Replace(' ', '_'), defaultWineStable },
                        { defaultWineLegacy.Name.Replace(' ', '_'), defaultWineLegacy },
                    }
                },
                {
                    "Dxvk", new Dictionary<string, CompatToolRelease>()
                    {
                        { defaultDxvkStable.Name.Replace(' ', '_'), defaultDxvkStable },
                        { defaultDxvkLegacy.Name.Replace(' ', '_'), defaultDxvkLegacy },
                    }
                }
            };
            WriteToolsToJSON(jsonFile);
        }
        else
        {
            ReadJSONToTools(jsonFile);
        }
        if(!Tools["Dxvk"].ContainsKey("Disabled"))
            Tools["Dxvk"].Add("Disabled", defaultDxvkDisabled);
        Initialized = true;
    }

    public static Dictionary<string, CompatToolRelease> GetToolList(string tooltype)
    {
        return Tools[tooltype];
    }
    
    public static CompatToolRelease GetTool(string tooltype, string toolname)
    {
        if (Tools[tooltype].ContainsKey(toolname))
            return Tools[tooltype][toolname];

        return new CompatToolRelease { Folder = ""};
    }
    public static CompatToolRelease ConvertToCompatToolRelease(IToolRelease release)
    {
        return new CompatToolRelease
        {
            Folder = release.Folder,
            DownloadUrl = release.DownloadUrl,
            TopLevelFolder = release.TopLevelFolder,
            Name = release.Name,
            Description = release.Description
        };
    }

    private static void WriteToolsToJSON(string filename)
    {
        if (Tools is null)
            throw new InvalidOperationException("CompatToolbox.Tools is empty. Can't generate a JSON.");
        Dictionary<string, List<CompatToolRelease>> toolbox = new Dictionary<string, List<CompatToolRelease>>();
        foreach (var tooltype in Tools)
        {
            var toollist = new List<CompatToolRelease>();
            foreach (var tool in tooltype.Value)
            {
                toollist.Add(tool.Value);
            }
            toolbox.Add(tooltype.Key, toollist);
        }
        var json = JsonConvert.SerializeObject(toolbox, Formatting.Indented).Replace($"-{wineDistroId.ToString()}-", "-{wineDistroId}-");
        File.WriteAllText(filename, json);
    }

    private static void ReadJSONToTools(string filename)
    {
        Tools = new Dictionary<string, Dictionary<string, CompatToolRelease>>();
        var json = File.ReadAllText(filename).Replace("-{wineDistroId}-", $"-{wineDistroId.ToString()}-");
        var toolbox = JsonConvert.DeserializeObject<Dictionary<string, List<CompatToolRelease>>>(json);
        foreach (var tooltype in toolbox)
        {
            var toollist = new Dictionary<string, CompatToolRelease>(); 
            foreach (var tool in tooltype.Value)
            {
                toollist.Add(tool.Name.Replace(' ', '_'), tool);
            }
            Tools.Add(tooltype.Key, toollist);
        }
    }
}