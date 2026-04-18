using Hexa.NET.ImGui;

using System.Numerics;
using System.Runtime.InteropServices;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<RBWineStartupType> startupTypeSetting;

    private WineSettingsEntry wineVersionSetting;

    private WineSettingsEntry protonVersionSetting;

    private SettingsEntry<string> wineCustomBinaryPath;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<RBWineStartupType>(Strings.WineInstallSetting, Strings.WineInstallSettingDescription,
                () => Program.Config.RB_WineStartupType ?? RBWineStartupType.Managed, x => Program.Config.RB_WineStartupType = x),

            wineVersionSetting = new WineSettingsEntry(Strings.WineVersionSetting, Strings.WineVersionSettingDescription, () => Program.Config.RB_WineVersion ?? Program.WineManager.DEFAULTWINE,
                x => Program.Config.RB_WineVersion = x, Program.WineManager.WineVersion, Program.WineManager.DEFAULTWINE )
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Managed,
            },

            protonVersionSetting = new WineSettingsEntry(Strings.ProtonVersionSetting, Strings.ProtonVersionSettingDescription, () => Program.Config.RB_ProtonVersion ?? Program.WineManager.DEFAULTPROTON,
                s => Program.Config.RB_ProtonVersion = s, Program.WineManager.ProtonVersion, Program.WineManager.DEFAULTPROTON)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Proton,
            },

            wineCustomBinaryPath = new SettingsEntry<string>(Strings.CustomWineOrProtonSetting, Strings.CustomWineOrProtonSetting,
                () => Program.Config.RB_WineBinaryPath, s => Program.Config.RB_WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Custom,
                CheckValidity = s =>
                {
                    if (WineSettings.IsValidWineBinaryPath(s) || WineSettings.IsValidProtonBinaryPath(s))
                    {
                        return null;
                    }
                    return Strings.CustomWineOrProtonInvalid;
                },
            },

            new SettingsEntry<RBUmuLauncherType>(Strings.UmuLauncherSetting, Strings.UmuLauncherSettingDescription, () => Program.Config.RB_UmuLauncher ?? RBUmuLauncherType.System, x => Program.Config.RB_UmuLauncher = x)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Proton || (startupTypeSetting.Value == RBWineStartupType.Custom && WineSettings.IsValidProtonBinaryPath(wineCustomBinaryPath.Value)),
                CheckWarning = x =>
                {
                    if (x != RBUmuLauncherType.Disabled && CoreEnvironmentSettings.IsAppImage)
                        return Strings.UmuLauncherWarning;
                    return null;
                }
            },

            new SettingsEntry<bool>(Strings.EnableFeralGameModeSetting, Strings.EnableFeralGameModeSettingDescription, () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckWarning = b =>
                {
                    if (b == true && FeralGameModeFound == false)
                        return Strings.EnableFeralGameModeNotFoundValidation;
                    return null;
                }
            },

            new SettingsEntry<RBWineSyncType>(Strings.WineSyncMethodSetting, Strings.WineSyncMethodSettingDescription, () => Program.Config.RB_WineSync ?? RBWineSyncType.FSync, x => Program.Config.RB_WineSync = x)
            {
                CheckValidity = b =>
                {
                    switch (WineUtility.SystemFsyncSupport())
                    {
                        case FsyncSupport.UnsupportedPlatform:
                            return Strings.EnableFsyncSettingUnsupportedPlatformValidation;
                        case FsyncSupport.OutdatedKernel:
                            return Strings.EnableFSyncSettingMinKernelValidation;
                        case FsyncSupport.Supported:
                        default:
                            return null;
                    }
                }
            },

            new SettingsEntry<bool>(Strings.WaylandSetting, Strings.WaylandSettingDescription, () => Program.Config.WaylandEnabled ?? false, b => Program.Config.WaylandEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<string>(Strings.WineDebugAdditionalVarSetting, Strings.WineDebugAdditionalVarSettingDescription, () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s),

            new SettingsEntry<bool>(Strings.GamescopeEnabled, Strings.GameScopeEnabledDescription, () => Program.Config.RB_GamescopeEnabled ?? false, b => Program.Config.RB_GamescopeEnabled = b)
            {
                CheckWarning = x => 
                {
                    if (!Program.IsGamescopeInstalled)
                        return Strings.GamescopeNotFound;
                    return null;
                },
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<string>(Strings.GamescopeArguments, Strings.GameScopeArgumentsDescription, () => Program.Config.RB_GamescopeArguments ?? "", s => Program.Config.RB_GamescopeArguments = s),
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => Strings.WineTitle;

    private bool? feralGameModeFound = null;

    private bool FeralGameModeFound
    {
        get
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
            if (feralGameModeFound != null) return feralGameModeFound ?? false;
            var handle = IntPtr.Zero;
            feralGameModeFound = (NativeLibrary.TryLoad("libgamemodeauto.so.0", out handle));
            NativeLibrary.Free(handle);
            return feralGameModeFound ?? false;
        }
    }

    public override void Draw()
    {
        if (Program.WineManager.IsListUpdated)
        {
            Program.WineManager.DoneUpdatingWineList();
            wineVersionSetting.Reset(Program.WineManager.WineVersion, Program.WineManager.DEFAULTWINE);
            protonVersionSetting.Reset(Program.WineManager.ProtonVersion, Program.WineManager.DEFAULTPROTON);
        }
       
        base.Draw();

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.BeginDisabled();
            ImGui.Text(Strings.CompatibilityToolNotSetup);

            ImGuiHelpers.ScaleDummy(10);
        }

        if (ImGui.Button(Strings.OpenWINEPrefix))
        {
            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Settings.Prefix.FullName);
        }

        ImGui.SameLine();

        if (ImGui.Button(Strings.OpenWINEConfiguration))
        {
            Program.CompatibilityTools.RunInPrefix("winecfg");
        }

        ImGui.SameLine();

        if (ImGui.Button(Strings.OpenWINEExplorer))
        {
            Program.CompatibilityTools.RunInPrefix("explorer");
        }

        if (ImGui.Button(Strings.KillAllWINEProcesses))
        {
            Program.CompatibilityTools.Kill();
        }

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.EndDisabled();
        }
    }

    public override void Save()
    {
        base.Save();
        Program.CreateCompatToolsInstance();
    }
}