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

        var items = new List<StackLayoutItem>();

        foreach (var control in this.HeaderContent)
            items.Add(new StackLayoutItem(control, expand: true));

        foreach (SettingsEntry settingsEntry in Entries)
        {
            if (settingsEntry.IsVisible)
                items.Add(new StackLayoutItem(settingsEntry, expand: true));
        }

        foreach (var control in this.FooterContent)
            items.Add(new StackLayoutItem(control, expand: true));

        var layout = new StackLayout { Spacing = 10 };

        foreach (var item in items)
            layout.Items.Add(item);

        this.Content = WrapScrollable(layout);
    }

    /// <summary>Wraps page content in a Scrollable on Linux only. Eto's WinForms Scrollable
    /// (CustomScrollable) loops forever when laid out inside a TabPage: OnLayout calls
    /// UpdateScrollSize, which sets the content MinimumSize, which fires resize -> PerformLayout
    /// -> OnLayout again; the UI thread never returns to the message loop ("Not Responding").</summary>
    internal static Control WrapScrollable(Control content)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? new Scrollable { Content = content } : content;

    /// <summary>Refreshes tab content that depends on other settings values.</summary>
    protected virtual void RefreshDynamic() { }
}
