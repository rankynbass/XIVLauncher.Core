using Eto.Forms;

namespace XIVLauncher.Core.Components.SettingsPage;

/// <summary>
/// A settings row: a name, an input control, a description, and validity/warning labels.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode settings entry, which drew its widgets and re-evaluated validity every
/// frame. Here the entry is built once; validity and warning labels are recomputed whenever the entry's
/// value changes, and the page recomputes the finish button from the resulting
/// <see cref="Changed"/> events.
/// </remarks>
public abstract class SettingsEntry : Component
{
    protected object? InternalValue;

    public abstract string Name { get; }

    public bool IsValid { get; protected set; } = true;

    public abstract bool IsVisible { get; }

    public abstract void LoadValues();

    public abstract void Save();

    /// <summary>Raises when the entry value changes, so the page can refresh the finish button.</summary>
    public event Action? Changed;

    protected void RaiseChanged() => this.Changed?.Invoke();
}
