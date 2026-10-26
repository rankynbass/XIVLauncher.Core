using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Core.Components;

namespace XIVLauncher.Core;

/// <summary>
/// The launcher window: hosts the current launcher page over the background artwork.
/// </summary>
/// <remarks>
/// Replaces the SDL window. The background sits under the page host, reproducing the previous
/// bottom-left logo layer. The title reproduces the SDL window title, including the nightly hash when
/// running a nightly build.
/// </remarks>
public class LauncherForm : Form
{
    private readonly Panel pageHost = new();

    public LauncherForm(LauncherApp app, string version, string hash)
    {
        this.Title = $"XIVLauncher {version} ({hash})";
        this.ClientSize = new Size(1280, 800);

        this.Content = new PixelLayout();

        var layout = (PixelLayout)this.Content;

        // The background artwork is added first so the page host paints over it. The window size is
        // fixed, so the fixed layout can place both children at the full window size.
        var background = new Background { Size = new Size(1280, 800) };
        layout.Add(background, 0, 0);

        this.pageHost.Size = new Size(1280, 800);
        layout.Add(this.pageHost, 0, 0);
    }

    /// <summary>Hosts the given page, replacing the currently displayed page. UI thread only.</summary>
    public void ShowPage(Page page)
    {
        Ui.AssertUIThread("LauncherForm.ShowPage");
        this.pageHost.Content = page;
    }

    public void ShowWindow()
    {
        Ui.AssertUIThread("LauncherForm.ShowWindow");
        this.Visible = true;
        this.WindowState = WindowState.Normal;
    }

    public void HideWindow()
    {
        Ui.AssertUIThread("LauncherForm.HideWindow");

        // The GTK GApplication exits when its last visible window closes, so the launcher cannot
        // hide the main window while the game starts. Minimising is the Eto equivalent of the SDL
        // window hide used by the login pipeline.
        this.WindowState = WindowState.Minimized;
    }

    /// <summary>The form that owns modal dialogs.</summary>
    public Window ModalOwner => this;
}
