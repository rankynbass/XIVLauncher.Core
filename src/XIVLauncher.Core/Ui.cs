using Eto.Forms;

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
