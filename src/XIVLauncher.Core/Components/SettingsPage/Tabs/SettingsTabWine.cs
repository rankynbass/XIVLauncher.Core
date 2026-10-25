using System.Runtime.InteropServices;

using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

/// <summary>
/// The Wine/DXVK settings tab, plus the compatibility tool actions.
/// </summary>
/// <remarks>
/// The action buttons and the "not set up" note are wrapped in a group that is enabled only when the
/// compatibility tool has been downloaded, which is the state the immediate-mode tab enforced with
/// <c>BeginDisabled</c>.
/// </remarks>
public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineStartupType> startupTypeSetting;

    private SettingsEntry<DxvkVersion> dxvkVersionSetting;

    private readonly Panel toolActionPanel;

    public SettingsTabWine()
    {
        this.Entries =
        [
            // WINE
            this.startupTypeSetting = new SettingsEntry<WineStartupType>(Strings.WineInstallSetting, Strings.WineInstallSettingDescription,
                () => Program.Config.WineStartupType ?? WineStartupType.Managed, x => Program.Config.WineStartupType = x),
            new SettingsEntry<WineManagedVersion>(Strings.WineVersionSetting, Strings.WineVersionSettingDescription, () => Program.Config.WineManagedVersion ?? WineManagedVersion.Stable,
                x => Program.Config.WineManagedVersion = x)
            {
                CheckVisibility = () => this.startupTypeSetting.Value == WineStartupType.Managed
            },
            new SettingsEntry<string>(Strings.WineBinaryPathSetting,
                Strings.WineBinarySettingDescription,
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => this.startupTypeSetting.Value == WineStartupType.Custom
            },
            new SettingsEntry<WineSyncType>(Strings.WineSyncMethodSetting, Strings.WineSyncMethodSettingDescription, () => Program.Config.WineSyncType ?? WineSyncType.FSync, x => Program.Config.WineSyncType = x)
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
            new SettingsEntry<string>(Strings.WineDebugAdditionalVarSetting, Strings.WineDebugAdditionalVarSettingDescription, () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s),

            // DXVK
            this.dxvkVersionSetting = new SettingsEntry<DxvkVersion>(Strings.DXVKVersionSetting, Strings.DXVKVersionSettingDescription, () => Program.Config.DxvkVersion ?? DxvkVersion.Stable, x => Program.Config.DxvkVersion = x),
            new SettingsEntry<DxvkHudType>(Strings.EnableDXVKOverlaySetting, Strings.EnableDXVKOverlaySettingDescription, () => Program.Config.DxvkHudType, type => Program.Config.DxvkHudType = type),
            new SettingsEntry<bool>(Strings.DXVKEnableAsyncSetting, Strings.DXVKEnableAsyncSettingDescription, () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b)
            {
                CheckVisibility = () => this.dxvkVersionSetting.Value != DxvkVersion.Disabled
            },

            // GameMode
            new SettingsEntry<bool>(Strings.EnableFeralGameModeSetting, Strings.EnableFeralGameModeSettingDescription, () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && FeralGameModeFound == false)
                        return Strings.EnableFeralGameModeNotFoundValidation;
                    return null;
                }
            },
        ];

        var notSetupLabel = new Label
        {
            Text = Strings.CompatibilityToolNotSetup,
            Wrap = WrapMode.Character,
        };

        var openPrefixButton = new Button { Text = Strings.OpenWINEPrefix };
        openPrefixButton.Click += (_, _) => PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Settings.Prefix.FullName);

        var openConfigurationButton = new Button { Text = Strings.OpenWINEConfiguration };
        openConfigurationButton.Click += (_, _) => Program.CompatibilityTools.RunInPrefix("winecfg");

        var openExplorerButton = new Button { Text = Strings.OpenWINEExplorer };
        openExplorerButton.Click += (_, _) => Program.CompatibilityTools.RunInPrefix("explorer");

        var killButton = new Button { Text = Strings.KillAllWINEProcesses };
        killButton.Click += (_, _) => Program.CompatibilityTools.Kill();

        this.toolActionPanel = new StackLayout
        {
            Spacing = 10,
            Items =
            {
                notSetupLabel,
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Items = { openPrefixButton, openConfigurationButton, openExplorerButton },
                },
                killButton,
            },
        };

        // Buttons cannot be disabled individually through a wrapping panel here, so the group state is
        // applied to each button.
        this.toolButtons = [openPrefixButton, openConfigurationButton, openExplorerButton, killButton];
        this.notSetupLabel = notSetupLabel;
    }

    private readonly Label notSetupLabel = null!;

    private readonly Button[] toolButtons = [];

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

    protected override List<Control> FooterContent => [this.toolActionPanel];

    protected override void RefreshDynamic()
    {
        var downloaded = Program.CompatibilityTools.IsToolDownloaded;

        this.notSetupLabel.Visible = !downloaded;

        foreach (var button in this.toolButtons)
            button.Enabled = downloaded;
    }

    public override void Save()
    {
        base.Save();
        Program.CreateCompatToolsInstance();
    }
}
