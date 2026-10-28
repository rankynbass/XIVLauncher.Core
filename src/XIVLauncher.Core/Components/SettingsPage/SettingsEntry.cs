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

    /// <summary>Rows gated out by the platform (they can never be enabled there) are hidden entirely
    /// at page build; conditional gates use <see cref="IsEnabled"/> so the row stays visible, disabled.</summary>
    public virtual bool IsVisible => true;

    /// <summary>Rows are always shown; a gated row reports false here and renders disabled.
    /// Gates are re-evaluated on every entry change via <see cref="RefreshState()"/>.</summary>
    public virtual bool IsEnabled => true;

    /// <summary>Recomputes validity, warning, and enabled state without raising <see cref="Changed"/>.</summary>
    public abstract void RefreshState();

    public abstract void LoadValues();

    public abstract void Save();

    /// <summary>Raises when the entry value changes, so the page can refresh the finish button.</summary>
    public event Action? Changed;

    protected void RaiseChanged() => this.Changed?.Invoke();
}
