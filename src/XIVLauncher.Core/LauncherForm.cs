using System.Runtime.InteropServices;

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
        this.MinimumSize = new Size(640, 480);

        this.Content = new PixelLayout();

        var layout = (PixelLayout)this.Content;

        // The background artwork is added first so the page host paints over it. The window is
        // resizable down to 640x480; the host and background follow the client size in the resize
        // handler, so the page lays out against the real window width.
        var background = new Background { Size = new Size(1280, 800) };
        layout.Add(background, 0, 0);

        this.pageHost.Size = new Size(1280, 800);
        layout.Add(this.pageHost, 0, 0);

        this.SizeChanged += (_, _) =>
        {
            var size = this.ClientSize;
            background.Size = size;
            this.pageHost.Size = size;
        };
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
        ReleaseApplication();
    }

    public void HideWindow()
    {
        Ui.AssertUIThread("LauncherForm.HideWindow");

        // The launcher stays alive while the game runs, with the window hidden (unmapped), not
        // minimised. Unmapping the main window drops the GApplication's window count, so the
        // application is held across the hidden period and released when the window comes back.
        HoldApplication();
        this.Visible = false;
    }

    /// <summary>The form that owns modal dialogs.</summary>
    public Window ModalOwner => this;

#if LINUX
    private static int appHolds;

    [DllImport("libgio-2.0.so.0")]
    private static extern void g_application_hold(IntPtr app);

    [DllImport("libgio-2.0.so.0")]
    private static extern void g_application_release(IntPtr app);

    /// <summary>The native GApplication behind Eto's GTK application handler. Eto keeps
    /// <c>Gtk.Application</c> on its internal handler, so it is read through reflection.</summary>
    private static IntPtr GApplicationHandle()
    {
        var handler = Application.Instance.Handler;
        var control = handler.GetType().GetProperty("Control")?.GetValue(handler);
        return control is null ? IntPtr.Zero : (IntPtr)control.GetType().GetProperty("Handle")!.GetValue(control)!;
    }

    private static void HoldApplication()
    {
        var handle = GApplicationHandle();
        if (handle == IntPtr.Zero)
            return;

        g_application_hold(handle);
        appHolds++;
    }

    private static void ReleaseApplication()
    {
        if (appHolds == 0)
            return;

        var handle = GApplicationHandle();
        if (handle == IntPtr.Zero)
            return;

        g_application_release(handle);
        appHolds--;
    }
#else
    // The GTK application-hold machinery keeps the GApplication alive while the main window is
    // unmapped. WinForms app lifetime is not tied to a window count, so hiding the form is safe
    // and the hold/release pair is a no-op on Windows.
    private static void HoldApplication() { }

    private static void ReleaseApplication() { }
#endif
}
