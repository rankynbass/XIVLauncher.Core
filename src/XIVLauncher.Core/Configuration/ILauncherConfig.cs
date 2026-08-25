using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;
using XIVLauncher.Common.Unix.Compatibility.Wine;

namespace XIVLauncher.Core.Configuration;

public interface ILauncherConfig
{
    public bool? CompletedFts { get; set; }

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

    public bool? IsEncryptArgs { get; set; }

    public bool? IsOtpServer { get; set; }

    public bool? IsIgnoringSteam { get; set; }

    public bool? UseDiscordRpcBridge { get; set; }
    
    public bool? DiscordRpcUseCustomPort { get; set; }

    public int? DiscordRpcCustomPort { get; set; }

    #region Patching

    public DirectoryInfo? PatchPath { get; set; }

    public bool? KeepPatches { get; set; }

    public long PatchSpeedLimit { get; set; }

    #endregion

    #region Linux

    // public WineStartupType? WineStartupType { get; set; }

    // public WineManagedVersion? WineManagedVersion { get; set; }

    // public string? WineBinaryPath { get; set; }

    public bool? GameModeEnabled { get; set; }

    // public DxvkVersion? DxvkVersion { get; set; }

    public bool? DxvkAsyncEnabled { get; set; }

    public bool? WaylandEnabled { get; set; }

    // public DxvkHudType DxvkHudType { get; set; }

    // public NvapiVersion? NvapiVersion { get; set; }

    public string? WineDebugVars { get; set; }

    public bool? FixLocale { get; set; }

    public bool? FixLDP { get; set; }

    public bool? FixIM { get; set; }

    public bool? FixError127 { get; set; }

    public bool? FixHideWineExports { get; set; }

    public bool? DontUseSystemTz { get; set; }

    public string? WineDLLOverrides { get; set; }

    #endregion

    #region RBpatched

    public RBWineStartupType? RB_WineStartupType { get; set; }

    public string? RB_WineVersion { get; set; }

    public string? RB_ProtonVersion { get; set; }

    public string? RB_WineBinaryPath { get; set; }

    public RBWineSyncType? RB_WineSync { get; set; }

    public string? RB_DxvkVersion { get; set; }

    public string? RB_NvapiVersion { get; set; }

    public bool? RB_GPLAsyncCacheEnabled { get; set; }

    public bool? RB_DxvkEnabled { get; set; }

    public bool? RB_NvapiEnabled { get; set; }

    public bool? RB_UseVulkanWineD3D { get; set; }

    public bool? RB_ProtonUseVulkanWineD3D { get; set; }

    public RBUmuLauncherType? RB_UmuLauncher { get; set; }

    public RBHudType? RB_HudType { get; set; }
    
    public string? RB_DxvkHudCustom { get; set; }

    public string? RB_MangoHudCustomFile { get; set; }

    public string? RB_MangoHudCustomString { get; set; }

    public int? RB_DxvkFrameRate { get; set; }

    public bool? RB_KeepToolsUpdated { get; set; }

    public bool? RB_GamescopeEnabled { get; set; }

    public string? RB_GamescopeArguments { get; set; }

    public string? RB_MangoHudArguments { get; set; }

    public bool? RB_ProtonLoggingEnabled { get; set; }

    public bool? RB_ProtonLoggingVerbose { get; set; }

    #endregion

    #region RBpatchedApps
    
    public string? RB_App1 { get; set; }

    public bool? RB_App1Enabled { get; set; }

    public string? RB_App1Args { get; set; }

    public bool? RB_App1WineD3D { get; set; }

    public string? RB_App2 { get; set; }

    public bool? RB_App2Enabled { get; set; }

    public string? RB_App2Args { get; set; }

    public bool? RB_App2WineD3D { get; set; }

    public string? RB_App3 { get; set; }

    public bool? RB_App3Enabled { get; set; }

    public string? RB_App3Args { get; set; }

    public bool? RB_App3WineD3D { get; set; }

    #endregion

    #region Dalamud

    public bool? DalamudEnabled { get; set; }

    public DalamudLoadMethod? DalamudLoadMethod { get; set; }
    public bool? DalamudManualInjectionEnabled { get; set; }
    public DirectoryInfo? DalamudManualInjectPath { get; set; }
    public string? DalamudBetaKind { get; set; }
    public string? DalamudBetaKey { get; set; }

    public int DalamudLoadDelay { get; set; }

    #endregion
}
