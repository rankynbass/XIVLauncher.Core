using System.Numerics;

using Hexa.NET.ImGui;

namespace XIVLauncher.Core.Components.SettingsPage;

public abstract class SettingsTab : Component
{
    public abstract SettingsEntry[] Entries { get; }

    public virtual bool IsUnixExclusive => false;

    public abstract string Title { get; }

    public override void Draw()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            if (settingsEntry.IsVisible)
            {
                settingsEntry.Draw();
                ImGuiHelpers.ScaleDummy(10);
            }
        }

        base.Draw();
    }

    public void Load()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            settingsEntry.Load();
        }
    }

    public virtual void Save()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            settingsEntry.Save();
        }
    }
}
