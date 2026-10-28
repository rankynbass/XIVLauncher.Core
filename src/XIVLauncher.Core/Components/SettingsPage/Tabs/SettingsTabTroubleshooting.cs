using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Common.Util;
using XIVLauncher.Core.Resources.Localization;
using XIVLauncher.Core.Support;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

/// <summary>
/// The troubleshooting tab: the Unix hack settings, the log-pack action, and the reset actions.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode tab, where every action was an ImGui button inside one draw pass.
/// Each action is now a button, the explanatory labels are grey (the old disabled-text style), and the
/// destructive-action warning is red.
/// </remarks>
public class SettingsTabTroubleshooting : SettingsTab
{
    public override SettingsEntry[] Entries { get; } =
    [
        new SettingsEntry<bool>(Strings.DisableGameoverlayRendererHack, Strings.DisableGameoverlayRendererHackDescription, () => Program.Config.FixLDP ?? false, x => Program.Config.FixLDP = x)
        {
            CheckEnabled = () => Environment.OSVersion.Platform == PlatformID.Unix,
        },
        new SettingsEntry<bool>(Strings.XModifiersHack, Strings.XModifiersHackDescription, () => Program.Config.FixIM ?? false, x => Program.Config.FixIM = x)
        {
            CheckEnabled = () => Environment.OSVersion.Platform == PlatformID.Unix,
        },
        new SettingsEntry<bool>(Strings.FixLibICUUCErrorHack, Strings.FixLibICUUCErrorHackDescription, () => Program.Config.FixError127 ?? false, x => Program.Config.FixError127 = x)
        {
            CheckEnabled = () => Environment.OSVersion.Platform == PlatformID.Unix,
        },
        new SettingsEntry<bool>(string.Format(Strings.ForceLocaleHack, (!string.IsNullOrEmpty(Program.CType) ? Program.CType : Strings.ForceLocaleHackCUTF8)),
                                !string.IsNullOrEmpty(Program.CType) ? string.Format(Strings.ForceLocaleHackDescription, Program.CType) : "Hack Disabled. Could not find a UTF-8 C locale. You may have to set LC_ALL manually if LANG is not a UTF-8 type.",
                                () => Program.Config.FixLocale ?? false, b => Program.Config.FixLocale = b)
        {
            CheckEnabled = () => Environment.OSVersion.Platform == PlatformID.Unix,
            CheckWarning = b =>
            {
                var lang = CoreEnvironmentSettings.GetCleanEnvironmentVariable("LANG");

                if (lang.ToUpper().Contains("UTF") && b)
                    return string.Format(Strings.ForceLocaleHackUTFValidation, lang);

                return null;
            },
        },
        new SettingsEntry<bool>(Strings.ForceDontUseSystemTZ, Strings.ForceDontUseSystemDescription, () => Program.Config.DontUseSystemTz ?? true, x => Program.Config.DontUseSystemTz = x)
        {
            CheckEnabled = () => Environment.OSVersion.Platform == PlatformID.Unix,
        },
    ];

    public override bool IsUnixExclusive => false;

    public override string Title => Strings.TroubleshootingTitle;

    public SettingsTabTroubleshooting()
    {
        this.headerItems = [new Label { Text = "Fixes", TextColor = EtoColors.Grey3 }];

        var openTsButton = new Button { Text = Strings.GenerateTSPackTroubleshootingButton };
        openTsButton.Click += (_, _) =>
        {
            PackGenerator.SavePack(Program.storage);
            PlatformHelpers.OpenBrowser(Path.Combine(Program.storage.GetFolder("logs").FullName));
        };

        var clearWineButton = new Button { Text = Strings.ClearWINEPrefixTroubleshootingButton };
        clearWineButton.Click += (_, _) => Program.ClearPrefix();

        var clearToolsButton = new Button { Text = Strings.ClearManagedCompatToolsTroubleshootingButton };
        clearToolsButton.Click += (_, _) => Program.ClearTools(true);

        var clearDalamudButton = new Button { Text = Strings.ClearDalamudTroubleshootingButton };
        clearDalamudButton.Click += (_, _) => Program.ClearPlugins(true);

        var clearLogsButton = new Button { Text = Strings.ClearAllLogsTroubleshootingButton };
        clearLogsButton.Click += (_, _) => Program.ClearLogs(true);

        var resetSettingsButton = new Button { Text = Strings.ResetSettingsTroubleshootingButton };
        resetSettingsButton.Click += (_, _) => Program.ClearSettings(true);

        var resetAllButton = new Button { Text = Strings.ResetAllTroubleshootingButton };
        resetAllButton.Click += (_, _) => Program.ClearAll(true);

        this.footerItems =
        [
            new Label { Text = "Logs", TextColor = EtoColors.Grey3 },
            new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Items = { openTsButton },
            },
            new Label
            {
                Text = Strings.GenerateTSPackTroubleshooting,
                TextColor = EtoColors.Grey3,
                Wrap = WrapMode.Character,
            },

            new Label { Text = "Cleanup", TextColor = EtoColors.Grey3 },
            new Label
            {
                Text = Strings.TroubleshootingDestructiveActionWarning,
                TextColor = EtoColors.Red,
                Wrap = WrapMode.Character,
            },
            new ActionRow(Strings.ClearWINEPrefixTroubleshooting, clearWineButton),
            new ActionRow(Strings.ClearManagedCompatToolsTroubleshooting, clearToolsButton),
            new ActionRow(Strings.ClearDalamudTroubleshooting, clearDalamudButton),
            new ActionRow(Strings.ClearAllLogsTroubleshooting, clearLogsButton),
            new ActionRow(Strings.ResetSettingsTroubleshooting, resetSettingsButton),
            new ActionRow(Strings.ResetAllTroubleshooting, resetAllButton),
        ];
    }

    private readonly List<Control> headerItems = [];
    private readonly List<Control> footerItems = [];

    protected override List<Control> HeaderContent => this.headerItems;

    protected override List<Control> FooterContent => this.footerItems;

    /// <summary>A button with its explanatory label, as the troubleshooting actions are laid out.</summary>
    private sealed class ActionRow : StackLayout
    {
        public ActionRow(string description, Button button)
        {
            this.Orientation = Orientation.Horizontal;
            this.Items.Add(button);
            this.Items.Add(new Label
            {
                Text = description,
                TextColor = EtoColors.Grey3,
                Wrap = WrapMode.Character,
            });
        }
    }
}
