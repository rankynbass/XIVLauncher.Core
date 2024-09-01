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

public static class ToolSetup
{
    public static string HOME => System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
    
    public static string STEAM => IsFlatpak ? Path.Combine(HOME, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam") : Path.Combine(HOME, ".local", "share", "Steam");

    public static bool IsFlatpak { get; set; } = false;

    public static DirectoryInfo CommonDir => new DirectoryInfo(Path.Combine(STEAM, "steamapps", "common"));

    public static DirectoryInfo CompatDir => new DirectoryInfo(Path.Combine(STEAM, "compatibilitytools.d"));

    public static bool IsSteamInstalled { get; private set; } = false;

    public static bool IsProton => Program.Config.WineType == WineType.Proton;

    public static string FolderName => Program.Config.WineType switch
    {
        WineType.Managed => Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine", Program.Config.WineVersion ?? Wine.GetDefaultVersion()),
        WineType.Custom => Program.Config.WineBinaryPath ?? "/usr/bin",
        WineType.Proton => Proton.GetVersionPath(Program.Config.ProtonVersion ?? ""),
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static string RuntimePath => IsProton ? Runtime.GetVersionPath(Program.Config.RuntimeVersion) : "";

    public static string RuntimeDownloadUrl => IsProton ? Runtime.GetDownloadUrl(Program.Config.RuntimeVersion) : "";

    public static string WineDownloadUrl => Program.Config.WineType switch
    {
        WineType.Managed => Wine.GetDownloadUrl(Program.Config.WineVersion),
        WineType.Proton => Proton.GetDownloadUrl(Program.Config.ProtonVersion),
        WineType.Custom => "",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static string NvapiFolderName => NvapiEnabled ? Program.Config.NvapiVersion ?? Dxvk.GetDefaultNvapiVersion() : "";

    public static string NvapiDownloadUrl => NvapiEnabled ? Dxvk.GetNvapiDownloadUrl(NvapiFolderName) : "";

    public static string NvngxFolderName => NvapiEnabled ? CoreEnvironmentSettings.NvidiaWineDLLPath() : "";

    public static bool NvapiEnabled => CoreEnvironmentSettings.IsDLSSAvailable && Program.Config.NvapiVersion != "DISABLED" && DxvkEnabled;

    public static string WineDLLOverrides => Program.Config.WineDLLOverrides ?? "";

    public static string DebugVars => Program.Config.WineDebugVars ?? "-all";

    public static FileInfo LogFile => new FileInfo(Path.Combine(Program.storage.GetFolder("logs").FullName, "wine.log"));

    public static DirectoryInfo Prefix => IsProton ? new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "protonprefix")) : new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "wineprefix"));

    public static bool ESyncEnabled => Program.Config.ESyncEnabled ?? true;

    public static bool FSyncEnabled => Program.Config.FSyncEnabled ?? false;

    public static int Dpi => ((Program.Config.WineScale ?? 100) > 400 || (Program.Config.WineScale ?? 100) < 100 || (Program.Config.WineScale ?? 100) % 25 != 0) ? 96 : (96 * (Program.Config.WineScale ?? 100)) / 100;

    public static bool DxvkEnabled => Program.Config.DxvkVersion != "DISABLED";

    public static string DxvkFolder => Program.Config.DxvkVersion ?? Dxvk.GetDefaultVersion();

    public static string DxvkDownloadUrl => Dxvk.GetDownloadUrl(Program.Config.DxvkVersion);

    public static int DxvkFrameRate => Program.Config.DxvkFrameRateLimit ?? 0;

    public static bool AsyncEnabled => Program.Config.DxvkAsyncEnabled ?? false;

    public static bool DxvkHudEnabled => Program.Config.DxvkHud != DxvkHud.None;    

    public static string DxvkHudString => Program.Config.DxvkHud switch
    {
        DxvkHud.None => "",
        DxvkHud.Custom => Program.Config.DxvkHudCustom ?? "1",
        DxvkHud.Default => "1",
        DxvkHud.Fps => "fps",
        DxvkHud.Full => "full",
        _ => throw new ArgumentOutOfRangeException(),

    };

    public static bool MangoHudEnabled => Program.Config.MangoHud != MangoHud.None;

    public static bool MangoHudCustomIsFile => Program.Config.MangoHud == MangoHud.CustomFile;

    public static string MangoHudString => Program.Config.MangoHud switch
    {
        MangoHud.None => "",
        MangoHud.Default => "",
        MangoHud.Full => "full",
        MangoHud.CustomString => Program.Config.MangoHudCustomString ?? "",
        MangoHud.CustomFile => Program.Config.MangoHudCustomFile ?? "",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public static void Initialize(bool isFlatpakSteam = false)
    {
        IsFlatpak = isFlatpakSteam;

        try
        {
            if (Directory.Exists(STEAM))
            {
                Log.Verbose($"Steam Root is {STEAM}");
                Log.Verbose($"Steam Common Directory is {CommonDir.FullName}");
                Log.Verbose($"Steam Compatibility Tools Directory is {CompatDir.FullName}");
                IsSteamInstalled = true;
            }
            else
            {
                throw new DirectoryNotFoundException($"Steam Root directory \"{STEAM}\" does not exist or is not a directory.");
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
    }
}

public enum WineType
{
    [SettingsDescription("Managed by XIVLauncher", "Choose a patched version of wine made specifically for XIVLauncher")]
    Managed,

    [SettingsDescription("Steam Runtime with Proton", "Use Valve's proton with a Steam runtime container.")]
    Proton,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}