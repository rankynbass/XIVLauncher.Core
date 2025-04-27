using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Http;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;
using XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class CompatToolbox
{
    private static DateTime DefaultTimestamp => new DateTime(2025, 4, 20, 0, 0, 0, DateTimeKind.Utc);
    
    private static string DefaultName => "Builtin XLCore tool list";

    private static string DefaultID => "Official";

    private static string DEFAULT_URL = "https://raw.githubusercontent.com/rankynbass/XIVLauncher.Core/refs/heads/simple-compat-rework/compatibilitytools.json";

    public static DateTime CurrentTimestamp { get; private set; }

    public static string CurrentName { get; private set; }

    public static string CurrentID { get; private set; }

    public static Dictionary<string, Dictionary<string, CompatToolRelease>> Tools { get; private set; }

    private static DirectoryInfo Storage;

    public static bool Initialized { get; private set; } = false;

    private static WineReleaseDistro wineDistroId;

    public static void Initialize(DirectoryInfo storageFolder)
    {
        Storage = storageFolder;
        wineDistroId = CompatUtil.GetWineIdForDistro();
        var jsonFile = Path.Combine(storageFolder.FullName, "compattools.json");
        
        if (!File.Exists(jsonFile))
        {
            Console.WriteLine("JSON DOES NOT EXIST");
            InitializeDefaultTools();
            WriteToolsToJSON(jsonFile);
        }
        else
        {
            Console.WriteLine("INITIALIZING TOOLS");
            var IsValid = ReadJSONToTools(jsonFile);
            Console.WriteLine("TOOLS INITIALIZED? " + IsValid.ToString());
            if (!IsValid)
            {
                InitializeDefaultTools();
                File.Delete(jsonFile);
                WriteToolsToJSON(jsonFile);
            }
        }
        Tools["Dxvk"]["Disabled"] = ConvertToCompatToolRelease(new DxvkDisabledRelease());
        Tools["Nvapi"]["Disabled"] = ConvertToCompatToolRelease(new NvapiDisabledRelease());
        Initialized = true;
    }

    private static void InitializeDefaultTools()
    {
        var defaultWineStable = ConvertToCompatToolRelease(new WineStableRelease(wineDistroId));
        var defaultWineLegacy = ConvertToCompatToolRelease(new WineLegacyRelease(wineDistroId));
        var defaultDxvkStable = ConvertToCompatToolRelease(new DxvkStableRelease());
        var defaultDxvkLegacy = ConvertToCompatToolRelease(new DxvkLegacyRelease());
        var defaultNvapiStable = ConvertToCompatToolRelease(new NvapiStableRelease());
        var defaultNvapiLegacy071 = ConvertToCompatToolRelease(new NvapiLegacyRelease071());
        var defaultNvapiLegacy060 = ConvertToCompatToolRelease(new NvapiLegacyRelease060());
        var defaultNvapiLegacy054 = ConvertToCompatToolRelease(new NvapiLegacyRelease054());

        CurrentID = DefaultID;
        CurrentName = DefaultName;
        CurrentTimestamp = DefaultTimestamp;

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
            },
            {
                "Nvapi", new Dictionary<string, CompatToolRelease>()
                {
                    { defaultNvapiStable.Name, defaultNvapiStable },
                    { defaultNvapiLegacy071.Name.Replace(' ', '_'), defaultNvapiLegacy071 },
                    { defaultNvapiLegacy060.Name.Replace(' ', '_'), defaultNvapiLegacy060 },
                    { defaultNvapiLegacy054.Name.Replace(' ', '_'), defaultNvapiLegacy054 },
                }
            }
        };
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

    private static void WriteToolsToJSON(string filename, string id = "", string name = "", string timestamp = "")
    {
        if (Tools is null)
            throw new InvalidOperationException("CompatToolbox.Tools is empty. Can't generate a JSON.");
        if (!Tools.ContainsKey("Wine") || !Tools.ContainsKey("Dxvk") || !Tools.ContainsKey("Nvapi"))
            throw new InvalidOperationException("CompatToolbox.Tools is missing critical tools. Can't generate a JSON.");

        if (string.IsNullOrEmpty(id))
            id = DefaultID;
        if (string.IsNullOrEmpty(name))
            name = DefaultName;
        if (string.IsNullOrEmpty(timestamp))
            timestamp = ToTimestamp(DefaultTimestamp);

        Console.WriteLine($"INITIALIZING JSON: {name}, {timestamp}");

        var toolbox = new CompatToolList(id, name, timestamp);

        foreach (var tool in Tools["Wine"])
        {
            toolbox.AddWine(tool.Value);
            Console.WriteLine($"Adding wine {tool.Value}");
        }
        foreach (var tool in Tools["Dxvk"])
            toolbox.AddDxvk(tool.Value);
        foreach (var tool in Tools["Nvapi"])
            toolbox.AddNvapi(tool.Value);

        var json = JsonConvert.SerializeObject(toolbox, Formatting.Indented).Replace($"-{wineDistroId.ToString()}-", "-{wineDistroId}-");
        File.WriteAllText(filename, json);
        Console.WriteLine(json);
    }

    private static bool ReadJSONToTools(string filename)
    {
        Console.WriteLine("READING JSON FILE");
        Tools = new Dictionary<string, Dictionary<string, CompatToolRelease>>();
        var json = File.ReadAllText(filename).Replace("-{wineDistroId}-", $"-{wineDistroId.ToString()}-");
        var toolbox = JsonConvert.DeserializeObject<CompatToolList>(json);

        var timestamp = ToDateTime(toolbox.Timestamp);
        if (timestamp is null)
        {
            Log.Warning($"Timestamp of json file ({toolbox.Timestamp}) is invalid.");
            return false;
        }

        if (ToDateTime(toolbox.Timestamp) < DefaultTimestamp && toolbox.ID == DefaultID)
        {
            Log.Warning($"Timestamp of json file ({toolbox.Timestamp}) is older than the default ({(ToTimestamp(DefaultTimestamp))})");
            return false;
        }
        var winetools = new Dictionary<string, CompatToolRelease>();
        foreach (var tool in toolbox.Wine)
        {
            var name = tool.Name.Replace(' ', '_');
            winetools.Add(name, tool);
            Console.WriteLine($"Adding {name}");
        }
        var dxvktools = new Dictionary<string, CompatToolRelease>();
        foreach (var tool in toolbox.Dxvk)
        {
            var name = tool.Name.Replace(' ', '_');
            dxvktools.Add(name, tool);
            Console.WriteLine($"Adding {name}");
        }
        var nvapitools = new Dictionary<string, CompatToolRelease>();
        foreach (var tool in toolbox.Nvapi)
        {
            var name = tool.Name.Replace(' ', '_');
            nvapitools.Add(name, tool);
            Console.WriteLine($"Adding {name}");
        }
        Tools.Add("Wine", winetools);
        Tools.Add("Dxvk", dxvktools);
        Tools.Add("Nvapi", nvapitools);

        Console.WriteLine(JsonConvert.SerializeObject(Tools, Formatting.Indented));

        CurrentID = toolbox.ID;
        CurrentName = toolbox.Name;
        CurrentTimestamp = timestamp ?? DefaultTimestamp;

        return true;
    }

    public static DateTime? ToDateTime(string timestamp)
    {
        Console.WriteLine($"TIMESTAMP: {timestamp}");
        var datetime = timestamp.Trim().Split(' ');
        if (datetime.Length != 2)
            return null;
        var date = datetime[0].Trim().Split('-');
        var time = datetime[1].Trim().Split(':');
        if (date.Length != 3)
            return null;
        if (time.Length != 3)
            return null;
        return new DateTime(int.Parse(date[0]), int.Parse(date[1]), int.Parse(date[2]),
            int.Parse(time[0]), int.Parse(time[1]), int.Parse(time[2]), DateTimeKind.Utc);
    }

    public static string ToTimestamp(DateTime timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static async Task GetLatestJSON(string url = "")
    {
        if (!Initialized)
            return;

        if (string.IsNullOrEmpty(url))
            url = DEFAULT_URL;
        
        using var client = new HttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(url).ConfigureAwait(false));
        var IsValid = ReadJSONToTools(tempPath);
        if (IsValid)
            File.Copy(tempPath, Path.Combine(Storage.FullName, "compattools.json"), true);

        File.Delete(tempPath);           
    }
}