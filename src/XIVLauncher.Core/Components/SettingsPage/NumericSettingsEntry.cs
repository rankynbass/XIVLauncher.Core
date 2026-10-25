using Eto.Forms;

namespace XIVLauncher.Core.Components.SettingsPage;

/// <summary>
/// A settings row for a bounded integer, drawn as a native numeric stepper.
/// </summary>
/// <remarks>
/// Replaces the ImGui integer input, which clamped the value in its own draw pass. The stepper
/// clamps through its own minimum/maximum, and the row clamps again on change so the stored value
/// stays inside the configured range.
/// </remarks>
public class NumericSettingsEntry : SettingsEntry<int>
{
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public int Step { get; set; }

    public NumericSettingsEntry(string name, string description, Func<int> load, Action<int> save, int minValue = 0, int maxValue = int.MaxValue, int step = 1)
        : base(name, description, load, save)
    {
        this.MinValue = minValue;
        this.MaxValue = maxValue;
        this.Step = step;
    }

    protected override Control CreateInput()
    {
        var stepper = new NumericStepper
        {
            MinValue = this.MinValue,
            MaxValue = this.MaxValue,
            Increment = this.Step,
        };

        this.applyValue = () => stepper.Value = this.Value;

        stepper.ValueChanged += (_, _) =>
        {
            this.InternalValue = Math.Max(this.MinValue, Math.Min(this.MaxValue, (int)stepper.Value));
            this.UpdateState();
        };

        return stepper;
    }
}
