using System.Diagnostics;

using Eto.Forms;

using Serilog;

namespace XIVLauncher.Core;

/// <summary>
/// Marshals work onto the Eto UI thread.
/// </summary>
/// <remarks>
/// The launcher runs login, patching and news loading on background threads. Eto controls may only be
/// touched from the UI thread, so every UI mutation performed by those threads goes through here.
/// <see cref="Run"/> blocks the calling thread until the action has completed on the UI thread,
/// which is what keeps <c>ShowMessageBlocking</c> blocking.
/// </remarks>
public static class Ui
{
    private static int uiThreadId = -1;

    /// <summary>Records the thread that will own the UI loop. Call once, on the thread that calls
    /// <c>Application.Run</c>, before the loop starts.</summary>
    public static void MarkUIThread() => uiThreadId = Environment.CurrentManagedThreadId;

    /// <summary>True on the thread that owns the UI loop, false (and -1-safe) before
    /// <see cref="MarkUIThread"/> runs.</summary>
    public static bool IsUIThread => uiThreadId == Environment.CurrentManagedThreadId;

    /// <summary>Debug-only guard for a widget write that must be on the UI thread: logs the offending
    /// managed stack when it is not. Eto/GTK is single-threaded, so an unmarshalled write corrupts pango/GTK
    /// layout state and segfaults (pango_shape_item, gtk_widget_get_preferred_width recursion) in a way a
    /// core dump reconstructs only as native frames, so the managed stack is the usable diagnosis.</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void AssertUIThread(string site)
    {
        if (IsUIThread)
            return;

        Log.Warning("{Site} must run on the UI thread\n{Stack}", site, Environment.StackTrace);
    }

    /// <summary>Debug-only: catches Eto's own "Accessing UI object from a non-UI thread" warning and logs
    /// the managed stack that triggered it, so an unmarshalled widget access is identifiable from
    /// launcher.log. Eto emits the warning from <c>Application.EnsureUIThread</c> with
    /// <c>Trace.WriteLine</c>, which a listener added to <see cref="Trace.Listeners"/> sees.</summary>
    public static void CaptureUIThreadWarnings() => Trace.Listeners.Add(new UIThreadWarningListener());

    private sealed class UIThreadWarningListener : TraceListener
    {
        public override void Write(string? message) { }

        public override void WriteLine(string? message)
        {
            if (message is null || !message.Contains("non-UI thread"))
                return;

            Log.Warning("Eto UI-object access from a non-UI thread\n{Stack}", Environment.StackTrace);
        }
    }

    /// <summary>Queues <paramref name="action"/> on the UI thread without waiting for it.</summary>
    public static void Post(Action action) => Application.Instance.AsyncInvoke(action);

    /// <summary>Runs <paramref name="action"/> on the UI thread, blocking the caller until it finished.</summary>
    public static void Run(Action action) => Application.Instance.Invoke(action);

    /// <summary>Runs <paramref name="func"/> on the UI thread and returns its result, blocking the caller.</summary>
    public static T Run<T>(Func<T> func)
    {
        var result = default(T)!;
        Application.Instance.Invoke(() => result = func());
        return result;
    }
}
