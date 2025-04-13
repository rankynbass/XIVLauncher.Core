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

    public static void Initialize(DirectoryInfo storageFolder)
    {
        Storage = storageFolder;
        var jsonFile = Path.Combine(storageFolder.FullName, "compattools.json");
        var wineDistroId = CompatUtil.GetWineIdForDistro();
        var defaultWineStable = ConvertToCompatToolRelease(new WineStableRelease(wineDistroId));
        var defaultWineLegacy = ConvertToCompatToolRelease(new WineLegacyRelease(wineDistroId));
        var defaultDxvkStable = ConvertToCompatToolRelease(new DxvkStableRelease());
        var defaultDxvkLegacy = ConvertToCompatToolRelease(new DxvkLegacyRelease());
        
        Console.WriteLine("WineStableRelease.Folder = " + defaultWineStable.Folder);

        if (!File.Exists(jsonFile))
        {
            Tools = new Dictionary<string, Dictionary<string, CompatToolRelease>>()
            {
                {
                    "Wine", new Dictionary<string, CompatToolRelease>()
                    { 
                        { defaultWineStable.Folder, defaultWineStable },
                        { defaultWineLegacy.Folder, defaultWineLegacy },
                    }
                },
                {
                    "Dxvk", new Dictionary<string, CompatToolRelease>()
                    {
                        { defaultDxvkStable.Folder, defaultDxvkStable },
                        { defaultDxvkLegacy.Folder, defaultDxvkLegacy },
                    }
                }
            };
            string json = JsonConvert.SerializeObject(Tools, Formatting.Indented);
            json = json.Replace($"-{wineDistroId.ToString()}-", "-{distro}-");
            File.WriteAllText(jsonFile, json);
        }
        else
        {
            string json = File.ReadAllText(jsonFile);
            json = json.Replace("-{distro}-", $"-{wineDistroId.ToString()}-");
            Tools = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, CompatToolRelease>>>(json);
        }
        Initialized = true;
    }

    public static Dictionary<string, CompatToolRelease> GetToolList(string toolType)
    {
        return Tools[toolType];
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

}