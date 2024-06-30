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

public static class ToolBuilder
{
    public const string DEFAULT_WINE = "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";

    public const string DEFAULT_PROTON = "GE-Proton8-9";

    public const string DEFAULT_RUNTIME = "SteamLinuxRuntime_sniper";

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
        WineType.Proton => Proton.GetVersionPath(Program.Config.ProtonVersion ?? DEFAULT_PROTON),
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

    public static string DebugVars => Program.Config.WineDebugVars ?? "-all";

    public static FileInfo LogFile => new FileInfo(Path.Combine(Program.storage.GetFolder("logs").FullName, "wine.log"));

    public static DirectoryInfo Prefix => IsProton ? new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "protonprefix")) : new DirectoryInfo(Path.Combine(Program.storage.Root.FullName, "wineprefix"));

    public static bool ESyncEnabled => Program.Config.ESyncEnabled ?? true;

    public static bool FSyncEnabled => Program.Config.FSyncEnabled ?? false;

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