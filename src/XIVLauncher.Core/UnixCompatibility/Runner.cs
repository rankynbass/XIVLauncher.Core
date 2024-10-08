using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.UnixCompatibility;

public static class Runner
{
    public static bool IsSteamInstalled { get; private set; }

    public static string Steam { get; private set; }

    public static DirectoryInfo CommonDir { get; private set; }

    public static DirectoryInfo CompatDir { get; private set; }

    public static bool IsProton => Program.Config.RunnerType == RunnerType.Proton;

    public static string FullName => IsProton ? Proton.GetVersion(Program.Config.ProtonVersion) : Wine.GetVersion(Program.Config.WineVersion);

    public static string DownloadUrl => IsProton ? Proton.GetDownloadUrl(Program.Config.ProtonVersion) : Wine.GetDownloadUrl(Program.Config.WineVersion);

    public static string RuntimeFullName => IsProton ? Runtime.GetVersion(Program.Config.RuntimeVersion) : "";

    public static string RuntimeDownloadUrl => IsProton ? Runtime.GetDownloadUrl(Program.Config.RuntimeVersion) : "";

    public static string WineDLLOverrides => Program.Config.WineDLLOverrides ?? "";

    public static string DebugVars => Program.Config.WineDebugVars ?? "-all";

    public static FileInfo LogFile => new FileInfo(Path.Combine(Program.storage.GetFolder("logs").FullName, "wine.log"));

    public static DirectoryInfo Prefix => IsProton ? new DirectoryInfo(CoreEnvironmentSettings.WinePrefix ?? Path.Combine(Program.storage.Root.FullName, "protonprefix")) : new DirectoryInfo(CoreEnvironmentSettings.WinePrefix ?? Path.Combine(Program.storage.Root.FullName, "wineprefix"));

    public static bool ESyncEnabled => Program.Config.ESyncEnabled ?? true;

    public static bool FSyncEnabled => Program.Config.FSyncEnabled ?? false;

    public static int Dpi => ((Program.Config.WineScale ?? 100) > 400 || (Program.Config.WineScale ?? 100) < 100 || (Program.Config.WineScale ?? 100) % 25 != 0) ? 96 : (96 * (Program.Config.WineScale ?? 100)) / 100;

    public static void Initialize()
    {
        Steam = (OSInfo.IsFlatpak && CoreEnvironmentSettings.IsSteamCompatTool) ? Program.Config.SteamFlatpakPath : Program.Config.SteamPath;
        CommonDir = new DirectoryInfo(Path.Combine(Steam, "steamapps", "common"));
        CompatDir = new DirectoryInfo(Path.Combine(Steam, "compatibilitytools.d"));

        try
        {
            if (Directory.Exists(Steam))
            {
                Log.Verbose($"Steam Root is {Steam}");
                Log.Verbose($"Steam Common Directory is {CommonDir.FullName}");
                Log.Verbose($"Steam Compatibility Tools Directory is {CompatDir.FullName}");
                IsSteamInstalled = true;
            }
            else
            {
                throw new DirectoryNotFoundException($"Steam Root directory \"{Steam}\" does not exist or is not a directory.");
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            Log.Error(ex, "No Steam directory found.");
            IsSteamInstalled = false;
        }
        
        Wine.Initialize();
        Proton.Initialize();
        Runtime.Initialize();
        Dxvk.Initialize();
        DLSS.Initialize();

        // Fix various versions in case they don't exist.
        Program.Config.WineVersion = Wine.GetVersion(Program.Config.WineVersion, false);
        Program.Config.ProtonVersion = Proton.GetVersion(Program.Config.ProtonVersion, false);
        Program.Config.RuntimeVersion = Runtime.GetVersion(Program.Config.RuntimeVersion, false);
        Program.Config.DxvkVersion = Dxvk.GetVersion(Program.Config.DxvkVersion);
        Program.Config.NvapiVersion = DLSS.GetVersion(Program.Config.NvapiVersion);
    }
}

public enum RunnerType
{
    [SettingsDescription("Managed by XIVLauncher", "Choose a version of wine from XIVLauncher's wine folder.")]
    Managed,

    [SettingsDescription("Proton with Steam Runtime", "Use Valve's Proton layer with a Steam runtime container.")]
    Proton,

    [SettingsDescription("Custom Wine", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}
