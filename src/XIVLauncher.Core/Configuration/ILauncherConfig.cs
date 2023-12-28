using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Configuration;

public interface ILauncherConfig
{
    public bool? CompletedFts { get; set; }

    public bool? DoVersionCheck { get; set; }

    public float? FontPxSize { get; set; }

    public string? CurrentAccountId { get; set; }

    public string? AcceptLanguage { get; set; }

    public bool? IsAutologin { get; set; }

    public DirectoryInfo? GamePath { get; set; }

    public DirectoryInfo? GameConfigPath { get; set; }

    public string? AdditionalArgs { get; set; }

    public ClientLanguage? ClientLanguage { get; set; }

    public bool? IsUidCacheEnabled { get; set; }

    public float? GlobalScale { get; set; }

    public DpiAwareness? DpiAwareness { get; set; }

    public bool? TreatNonZeroExitCodeAsFailure { get; set; }

    public List<AddonEntry>? Addons { get; set; }

    public bool? IsDx11 { get; set; }

    public bool? IsEncryptArgs { get; set; }

    public bool? IsFt { get; set; }

    public bool? IsOtpServer { get; set; }

    public bool? IsIgnoringSteam { get; set; }

    #region Patching

    public DirectoryInfo? PatchPath { get; set; }

    public bool? KeepPatches { get; set; }

    public AcquisitionMethod? PatchAcquisitionMethod { get; set; }

    public long PatchSpeedLimit { get; set; }

    #endregion

    #region Linux

    public WineType? WineType { get; set; }

    public string? WineVersion { get; set; }

    public string? WineBinaryPath { get; set; }

    public bool? GameModeEnabled { get; set; }

    public bool? DxvkAsyncEnabled { get; set; }

    public bool? DxvkGPLAsyncCacheEnabled { get; set; }

    public bool? ESyncEnabled { get; set; }

    public bool? FSyncEnabled { get; set; }

    public string? DxvkVersion { get; set; }

    public int? DxvkFrameRateLimit { get; set; }

    public DxvkHud? DxvkHud { get; set; }

    public string? DxvkHudCustom { get; set; }

    public MangoHud? MangoHud { get; set; }

    public string? MangoHudCustomString { get; set; }

    public string? MangoHudCustomFile { get; set; }

    public string? WineDebugVars { get; set; }

    public string? ProtonVersion { get; set; }

    public string? SteamRuntime { get; set; }
    
    public string? FixLocale { get; set; }

    public bool? FixLDP { get; set; }

    public bool? FixIM { get; set; }

    public string? HelperApp1 { get; set; }

    public string? HelperApp2 { get; set; }

    public string? HelperApp3 { get; set; }

    public string? HelperApp1Args { get; set; }

    public string? HelperApp2Args { get; set; }

    public string? HelperApp3Args { get; set; }

    public bool? HelperApp1Enabled { get; set; }

    public bool? HelperApp2Enabled { get; set; }

    public bool? HelperApp3Enabled { get; set; }

    public bool? HelperApp1WineD3D { get; set; }

    public bool? HelperApp2WineD3D { get; set; }

    public bool? HelperApp3WineD3D { get; set; }

    public string? SteamPath { get; set; }

    public string? SteamFlatpakPath { get; set; }

    #endregion

    #region Dalamud

    public bool? DalamudEnabled { get; set; }

    public DalamudLoadMethod? DalamudLoadMethod { get; set; }

    public int DalamudLoadDelay { get; set; }

    #endregion
}