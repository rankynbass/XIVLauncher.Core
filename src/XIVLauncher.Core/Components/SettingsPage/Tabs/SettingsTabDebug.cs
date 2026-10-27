using System.Collections;

using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

/// <summary>
/// The debug tab: generic launcher information and the environment variable table.
/// </summary>
/// <remarks>
/// Replaces the ImGui table, which was rebuilt inside every draw pass. The Eto table is rebuilt when
/// the tab is refreshed; the environment variables come from <see cref="Environment"/>, as in HEAD,
/// sorted by key.
/// </remarks>
public class SettingsTabDebug : SettingsTab
{
    private readonly Label steamDeckLabel = new() { Text = "Steam Deck Hardware Detected", Visible = false };
    private readonly TableLayout envVarsTable;

    public override SettingsEntry[] Entries => [];

    public override string Title => Strings.DebugTitle;

    public SettingsTabDebug()
    {
        this.envVarsTable = new TableLayout
        {
            Rows =
            {
                new TableRow(new Label { Text = "Key" }, new Label { Text = "Value" }),
            },
        };
    }

    protected override List<Control> HeaderContent =>
    [
        new Label { Text = "Generic Information", TextColor = EtoColors.Grey3 },
        new Label { Text = $"Operating System: {Environment.OSVersion}" },
        new Label { Text = $"Runtime Version: {Environment.Version}" },
        this.steamDeckLabel,
#if FLATPAK
        new Label { Text = "Running as a Flatpak" },
#endif
        new Label { Text = "Environment Information", TextColor = EtoColors.Grey3 },
        SettingsTab.WrapScrollable(this.envVarsTable),
    ];

    protected override void RefreshDynamic()
    {
        this.steamDeckLabel.Visible = Program.IsSteamDeckHardware;

        // Rebuild the environment table: the row set is cleared and repopulated, as the ImGui table was
        // on every draw.
        this.envVarsTable.Rows.Clear();
        this.envVarsTable.Rows.Add(new TableRow(new Label { Text = "Key" }, new Label { Text = "Value" }));

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().OrderBy(entry => entry.Key.ToString()))
            this.envVarsTable.Rows.Add(new TableRow(new Label { Text = entry.Key?.ToString() ?? "null" }, new Label { Text = entry.Value?.ToString() ?? "null" }));
    }
}
