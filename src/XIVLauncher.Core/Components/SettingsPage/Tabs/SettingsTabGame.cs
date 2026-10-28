using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Common;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

/// <summary>
/// The game settings tab: game path, configuration path, arguments, language, DPI, authentication
/// options, plus the UID cache reset action.
/// </summary>
/// <remarks>
/// The reset section is shown when the UID cache setting is enabled, as the immediate-mode tab checked
/// on every frame.
/// </remarks>
public class SettingsTabGame : SettingsTab
{
    private readonly Panel resetUidPanel;

    public override SettingsEntry[] Entries { get; } =
    [
        new SettingsEntry<DirectoryInfo>(Strings.GamePathSetting, Strings.GamePathSettingDescription, () => Program.Config.GamePath, x => Program.Config.GamePath = x)
        {
            CheckValidity = x =>
            {
                if (string.IsNullOrWhiteSpace(x?.FullName))
                    return Strings.GamePathSettingNotSetValidation;

                if (x.Name is "game" or "boot")
                    return Strings.GamePathSettingInvalidValidationj;

                return null;
            }
        },

        new SettingsEntry<DirectoryInfo>(Strings.GameConfigurationPathSetting, Strings.GameConfigurationPathSettingDescription, () => Program.Config.GameConfigPath, x => Program.Config.GameConfigPath = x)
        {
            CheckValidity = x => string.IsNullOrWhiteSpace(x?.FullName) ? Strings.GameConfigurationPathNotSetValidation : null,

            // TODO: We should also support this on Windows
            CheckVisibility = () => Environment.OSVersion.Platform == PlatformID.Unix,
        },

        new SettingsEntry<string>(Strings.AdditionalGameArgsSetting, Strings.AdditionalGameArgsSettingDescription, () => Program.Config.AdditionalArgs, x => Program.Config.AdditionalArgs = x),
        new SettingsEntry<ClientLanguage>(Strings.GameLanguageSetting, Strings.GameLanguageSettingDescription, () => Program.Config.ClientLanguage ?? ClientLanguage.English, x => Program.Config.ClientLanguage = x),
        new SettingsEntry<DpiAwareness>(Strings.GameDPIAwarenessSetting, Strings.GameDPIAwarenessSettingDescription, () => Program.Config.DpiAwareness ?? DpiAwareness.Unaware, x => Program.Config.DpiAwareness = x),
        new SettingsEntry<bool>(Strings.UseXLAuthMacrosSetting, Strings.UseXLAuthMacrosSettingDescription, () => Program.Config.IsOtpServer ?? false, x => Program.Config.IsOtpServer = x),
        new SettingsEntry<bool>(Strings.IgnoreSteamSetting, Strings.IgnoreSteamSettingDescription, () => Program.Config.IsIgnoringSteam ?? false, x => Program.Config.IsIgnoringSteam = x)
        {
            CheckVisibility = () => !CoreEnvironmentSettings.IsSteamCompatTool,
        },
        new SettingsEntry<bool>(Strings.UseUIDCacheSetting, Strings.UseUIDCacheSettingDescription, () => Program.Config.IsUidCacheEnabled ?? false, x => Program.Config.IsUidCacheEnabled = x),
    ];

    public override string Title => Strings.GameTitle;

    public SettingsTabGame()
    {
        var resetUidButton = new Button { Text = Strings.ResetUIDCacheButton };
        resetUidButton.Click += (_, _) => Program.ResetUIDCache();

        var uidLayout = new DynamicLayout { Spacing = new Size(10, 10) };
        uidLayout.Add(new Label { Text = Strings.ResetUIDCacheInfo, Wrap = WrapMode.Character });
        uidLayout.Add(resetUidButton);
        this.resetUidPanel = uidLayout;
    }

    protected override List<Control> FooterContent => [this.resetUidPanel];

    protected override void RefreshDynamic()
    {
        this.resetUidPanel.Visible = Program.Config.IsUidCacheEnabled == true;
    }
}
