using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.UnixCompatibility;

public enum DxvkHudType
{
    [SettingsDescription("None", "Show nothing")]
    None,

    [SettingsDescription("FPS", "Only show FPS")]
    Fps,

    [SettingsDescription("Custom", "Use a custom DXVK_HUD string")]
    Custom,

    [SettingsDescription("Full", "Show everything")]
    Full,
}

public static class DxvkManager
{
    private const string ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";

    private const string ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

    public static DxvkSettings GetSettings()
    {
        var isDxvk = true;
        var folder = "dxvk-async-1.10.3";
        var url = "https://github.com/Sporif/dxvk-async/releases/download/1.10.3/dxvk-async-1.10.3.tar.gz";
        var rootfolder = Program.storage.Root.FullName;
        var dxvkfolder = Path.Combine(rootfolder, "compatibilitytool", "dxvk");
        var async = (Program.Config.DxvkAsyncEnabled ?? true) ? "1" : "0";
        var env = new Dictionary<string, string>
        {
            { "DXVK_LOG_PATH", Path.Combine(rootfolder, "logs") },
            { "DXVK_CONFIG_FILE", Path.Combine(dxvkfolder, "dxvk.conf") },
        };

        if (isDxvk)
        {
            var dxvkCachePath = new DirectoryInfo(Path.Combine(dxvkfolder, "cache"));
            if (!dxvkCachePath.Exists) dxvkCachePath.Create();
            env.Add("DXVK_STATE_CACHE_PATH", Path.Combine(dxvkCachePath.FullName, folder));
        }

        var dxvkHudCustom = Program.Config.DxvkHudCustom ?? "fps,frametimes,gpuload,version";
        var hudType = Program.Config.DxvkHudType ?? DxvkHudType.None;
        switch (hudType)
        {
             case DxvkHudType.Fps:
                env.Add("DXVK_HUD","fps");
                break;

            case DxvkHudType.Custom:
                if (!CheckDxvkHudString(Program.Config.DxvkHudCustom))
                    dxvkHudCustom = "fps,frametimes,gpuload,version";
                env.Add("DXVK_HUD", Program.Config.DxvkHudCustom);
                break;

            case DxvkHudType.Full:
                env.Add("DXVK_HUD","full");
                break;

            case DxvkHudType.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var settings = new DxvkSettings(folder, url, Program.storage.Root.FullName, env, isDxvk);
        return settings;
    }

    public static bool CheckDxvkHudString(string? customHud)
    {
        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (customHud == "1") return true;
        if (!Regex.IsMatch(customHud,ALLOWED_CHARS)) return false;

        string[] hudvars = customHud.Split(",");

        return hudvars.All(hudvar => Regex.IsMatch(hudvar, ALLOWED_WORDS));
    }
}

