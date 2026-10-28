using System.Runtime.InteropServices;

using Eto.Drawing;
using Eto.Forms;

namespace XIVLauncher.Core.Components.SettingsPage;

/// <summary>
/// A settings tab: its entries, plus any tab-specific widgets drawn before and after them.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode tab, which drew each visible entry every frame. The entries and the
/// header/footer widgets are built once; the tab rebuilds its layout when the page is shown so
/// visibility checks (which depend on other settings) run at the same point they used to.
/// </remarks>
public abstract class SettingsTab : Component
{
    public abstract SettingsEntry[] Entries { get; }

    public virtual bool IsUnixExclusive => false;

    public abstract string Title { get; }

    /// <summary>Widgets drawn above the entries.</summary>
    protected virtual List<Control> HeaderContent => [];

    /// <summary>Widgets drawn below the entries.</summary>
    protected virtual List<Control> FooterContent => [];

    public void LoadValues()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            settingsEntry.LoadValues();
        }
    }

    public virtual void Save()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            settingsEntry.Save();
        }
    }

    /// <summary>Rebuilds the tab content from the currently visible entries.</summary>
    public void RefreshEntries()
    {
        this.RefreshDynamic();

        var layout = new DynamicLayout { Spacing = new Size(10, 10) };

        foreach (var control in this.HeaderContent)
            layout.Add(control);

        foreach (SettingsEntry settingsEntry in Entries)
        {
            if (settingsEntry.IsVisible)
                layout.Add(settingsEntry);
        }

        foreach (var control in this.FooterContent)
            layout.Add(control);

        // Content floor: drives the Scrollable's vertical scrollbars. Below this size, the tab
        // content scrolls instead of being clipped.
        layout.MinimumSize = new Size(500, 500);

        this.Content = layout;
    }

    /// <summary>Refreshes tab content that depends on other settings values.</summary>
    protected virtual void RefreshDynamic() { }
}
