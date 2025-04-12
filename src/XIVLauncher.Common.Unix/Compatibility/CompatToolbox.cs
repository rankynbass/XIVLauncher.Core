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
    public static Dictionary<string, List<IToolRelease>> Tools;

    private static DirectoryInfo Storage;

    public static void Initialize(DirectoryInfo storageFolder)
    {
        Storage = storageFolder;
        var jsonFile = Path.Combine(storageFolder.FullName, "compattools.json");
        var wineDistroId = CompatUtil.GetWineIdForDistro();

        if (!File.Exists(jsonFile))
        {
            Tools = new Dictionary<string, List<IToolRelease>>()
            {
                { "Wine", new List<IToolRelease>() { new WineStableRelease(wineDistroId), new WineLegacyRelease(wineDistroId) } },
                { "Dxvk", new List<IToolRelease>() { new DxvkStableRelease(), new DxvkLegacyRelease() } },
            };
            string json = JsonConvert.SerializeObject(Tools, Formatting.Indented);
            json = json.Replace($"-{wineDistroId.ToString()}-", "-{distro}-");
            File.WriteAllText(jsonFile, json);
        }
        else
        {
            string json = File.ReadAllText(jsonFile);
            json = json.Replace("-{distro}-", $"-{wineDistroId.ToString()}-");
            Tools = JsonConvert.DeserializeObject<Dictionary<string, List<IToolRelease>>>(json);
        }
    }
}