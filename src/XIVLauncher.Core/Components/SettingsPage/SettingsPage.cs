using System.Collections.Immutable;
using System.Runtime.InteropServices;

using Eto.Drawing;
using Eto.Forms;

using Serilog;

using XIVLauncher.Core.Components.SettingsPage.Tabs;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.SettingsPage;

/// <summary>
/// The settings page: the tab list, a search box, and the finish button.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode settings page. Tab visibility, entry visibility, and the finish button's
/// enabled state were recomputed by the draw pass; here they are recomputed when the page is shown,
/// when the search text changes, and when an entry reports a value change.
///
/// The search box filters entries across all tabs. Matching entries are re-parented into a results
/// panel, which mirrors the old "Search Results" tab; rebuilding a tab's layout returns them to it.
/// </remarks>
public class SettingsPage : Page
{
    private readonly SettingsTab[] tabs = BuildTabs();

    /// <summary>Builds the tab list. The Wine tab is built only on Linux: its entries, validators,
    /// footer actions, and Save() reference XIVLauncher.Common.Unix types (Program.CompatibilityTools,
    /// WineUtility, Wine/Dxvk enums), which do not exist on Windows — Program.CompatibilityTools is
    /// null there, so constructing the tab would dereference Unix state.</summary>
    private static SettingsTab[] BuildTabs()
    {
        var list = new List<SettingsTab>
        {
            new SettingsTabGame(),
            new SettingsTabPatching(),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            list.Add(new SettingsTabWine());

        list.Add(new SettingsTabDalamud());
        list.Add(new SettingsTabAbout());
        list.Add(new SettingsTabDebug());

        // Troubleshooting is Linux-only: every entry is a Unix hack, and the cleanup actions
        // (ClearPrefix, ClearTools) operate on Program.CompatibilityTools, which is null on Windows.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            list.Add(new SettingsTabTroubleshooting());

        return [.. list];
    }

    private readonly TabControl tabControl = new();
    private readonly TextBox searchBox;
    private readonly Common.Button finishButton;

    private string searchInput = string.Empty;

    public SettingsPage(LauncherApp app) : base(app)
    {
        this.searchBox = new TextBox
        {
            PlaceholderText = Strings.SearchForSettings,
            MaxLength = 100,
            Width = 250,
        };

        this.searchBox.TextChanged += (_, _) =>
        {
            this.searchInput = this.searchBox.Text;
            this.RebuildPages();
        };

        this.finishButton = new Common.Button(string.Empty) { Width = 40 };
        this.finishButton.ShowIcon(FontAwesomeIcon.Check);

        this.finishButton.Click += () =>
        {
            foreach (SettingsTab tab in this.tabs)
            {
                tab.Save();
            }

            this.App.State = LauncherApp.LauncherState.Main;
        };

        foreach (SettingsTab tab in this.tabs)
            foreach (SettingsEntry entry in tab.Entries)
                entry.Changed += this.UpdateFinishButton;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // The tab strip must be multiline, as six tab titles overflow a single row; DynamicLayout
            // stretches the tab control to the layout width, so no explicit width is needed.
            var winformsTab = this.tabControl.Handler.GetType().GetProperty("Control")?.GetValue(this.tabControl.Handler);
            winformsTab?.GetType().GetProperty("Multiline")?.SetValue(winformsTab, true);
        }

        var pageLayout = new DynamicLayout
        {
            Padding = new Padding(10),
            Spacing = new Size(10, 10),
        };

        pageLayout.AddCentered(this.searchBox);
        pageLayout.Add(this.tabControl, xscale: true, yscale: true);
        pageLayout.AddCentered(this.finishButton);

        this.Content = pageLayout;
    }

    public override void OnShow()
    {
        Log.Debug("[settings] OnShow: loading values");

        foreach (SettingsTab tab in this.tabs)
            tab.LoadValues();

        this.searchInput = string.Empty;
        this.searchBox.Text = string.Empty;

        Log.Debug("[settings] OnShow: rebuilding pages");
        this.RebuildPages();
        Log.Debug("[settings] pages rebuilt");

        base.OnShow();
    }

    private IEnumerable<SettingsTab> VisibleTabs()
    {
        foreach (SettingsTab tab in this.tabs)
        {
            if (!tab.IsUnixExclusive || Environment.OSVersion.Platform == PlatformID.Unix)
                yield return tab;
        }
    }

    private void RebuildPages()
    {
        this.tabControl.Pages.Clear();

        if (string.IsNullOrEmpty(this.searchInput))
        {
            foreach (SettingsTab tab in VisibleTabs())
            {
                Log.Debug("[settings] refreshing tab {Title}", tab.Title);
                tab.RefreshEntries();
                var page = new TabPage
                {
                    Text = tab.Title,
                    Content = new Scrollable
                    {
                        Content = tab,
                        Border = BorderType.None,
                        ExpandContentHeight = true,
                        ExpandContentWidth = false,
                    },
                };
                EnableNativeAutoScroll(page);
                this.tabControl.Pages.Add(page);
                Log.Debug("[settings] tab {Title} added to the TabControl", tab.Title);
            }
        }
        else
        {
            var results = new DynamicLayout { Spacing = new Size(10, 10) };
            results.MinimumSize = new Size(500, 500);
            var any = false;

            foreach (SettingsTab tab in VisibleTabs())
            {
                var eligible = tab.Entries.Where(x => x.Name.Contains(this.searchInput.Trim(), StringComparison.InvariantCultureIgnoreCase)).ToImmutableArray();

                if (!eligible.Any())
                    continue;

                any = true;

                results.Add(new Label { Text = tab.Title, TextColor = EtoColors.Grey3 });

                foreach (SettingsEntry settingsEntry in eligible)
                    if (settingsEntry.IsVisible)
                        results.Add(settingsEntry);
            }

            if (!any)
                results.Add(new Label { Text = Strings.NoResultsFound, TextColor = EtoColors.Grey3 });

            var resultPage = new TabPage
            {
                Text = Strings.SearchResults,
                Content = new Scrollable
                {
                    Content = results,
                    Border = BorderType.None,
                    ExpandContentHeight = true,
                    ExpandContentWidth = false,
                },
            };
            EnableNativeAutoScroll(resultPage);
            this.tabControl.Pages.Add(resultPage);
        }

        this.UpdateFinishButton();
    }

    /// <summary>Enables AutoScroll on the native WinForms TabPage: a TabPage is a Panel, and
    /// AutoScroll shows scrollbars when its content exceeds the page. Eto's Scrollable cannot be
    /// used on WinForms (its CustomScrollable layout loop hangs the UI). The content's
    /// MinimumSize is the floor that triggers the scrollbars.</summary>
    private static void EnableNativeAutoScroll(TabPage page)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var native = page.Handler?.GetType().GetProperty("Control")?.GetValue(page.Handler);
        native?.GetType().GetProperty("AutoScroll")?.SetValue(native, true);
    }

    private void UpdateFinishButton()
    {
        var invalid = this.tabs.Any(x => x.Entries.Any(y => y.IsVisible && !y.IsValid));

        this.finishButton.IsEnabled = !invalid;
        this.finishButton.ShowIcon(invalid ? FontAwesomeIcon.Ban : FontAwesomeIcon.Check);
    }
}
