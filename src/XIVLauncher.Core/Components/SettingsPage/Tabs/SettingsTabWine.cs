using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

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

            protonVersionSetting = new WineSettingsEntry("Proton Version", "Choose which Proton version to use. You may need to scroll down in menu to see custom versions.", () => Program.Config.RB_ProtonVersion ?? Program.WineManager.DEFAULTPROTON,
                s => Program.Config.RB_ProtonVersion = s, Program.WineManager.ProtonVersion, Program.WineManager.DEFAULTPROTON)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Proton,
            },

            wineCustomBinaryPath = new SettingsEntry<string>("Wine or Proton Binary Path",
                "Set the path XIVLauncher will use to run applications via Wine/Proton.\nIt should be an absolute path to a folder containing wine and/or win64 and wineserver binaries, or the proton binary.",
                () => Program.Config.RB_WineBinaryPath, s => Program.Config.RB_WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Custom,
                CheckValidity = s =>
                {
                    if (WineSettings.IsValidWineBinaryPath(s) || WineSettings.IsValidProtonBinaryPath(s))
                    {
                        return null;
                    }
                    return "Invalid wine or proton path.";
                },
            },

            new SettingsEntry<RBUmuLauncherType>("Umu Launcher", "Use Umu Launcher to run Proton inside the Steam Runtime container (recommended).", () => Program.Config.RB_UmuLauncher ?? RBUmuLauncherType.System, x => Program.Config.RB_UmuLauncher = x)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Proton || (startupTypeSetting.Value == RBWineStartupType.Custom && WineSettings.IsValidProtonBinaryPath(wineCustomBinaryPath.Value)),
                CheckWarning = x =>
                {
                    if (x != RBUmuLauncherType.Disabled && CoreEnvironmentSettings.IsAppImage)
                        return "AppImage Users: If you have issues with your system hanging while using Proton, set this to Disabled.";
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

            new SettingsEntry<bool>(Strings.EnableESyncSetting, Strings.EnableESyncSettingDescription, () => Program.Config.ESyncEnabled ?? true, b => Program.Config.ESyncEnabled = b),
            new SettingsEntry<bool>(Strings.EnableFSyncSetting, Strings.EnableFSyncSettingDescription, () => Program.Config.FSyncEnabled ?? true, b => Program.Config.FSyncEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && (Environment.OSVersion.Version.Major < 5 && (Environment.OSVersion.Version.Minor < 16 || Environment.OSVersion.Version.Major < 6)))
                        return Strings.EnableFSyncSettingMinKernelValidation;

                    return null;
                }
            },

            new SettingsEntry<bool>("Enable NTSync", "Enable NTSync. Requires a compatible kernel.", () => Program.Config.NTSyncEnabled ?? false, b => Program.Config.NTSyncEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<bool>("Enable Wayland Driver", "Enable Wine's experimental Wayland Driver.", () => Program.Config.WaylandEnabled ?? false, b => Program.Config.WaylandEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            },

            new SettingsEntry<bool>(Strings.SetWindows7Setting, Strings.SetWindows7SettingDescription, () => Program.Config.SetWin7 ?? true, b => Program.Config.SetWin7 = b),

            new SettingsEntry<string>(Strings.WineDebugAdditionalVarSetting, Strings.WineDebugAdditionalVarSettingDescription, () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
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

            ImGui.Dummy(new Vector2(10));
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
