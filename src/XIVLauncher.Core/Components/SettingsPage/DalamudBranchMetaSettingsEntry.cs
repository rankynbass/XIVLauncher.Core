using Eto.Forms;

using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Core.Components.SettingsPage;

/// <summary>
/// A settings row listing the Dalamud branches, filled in once the branch metadata has loaded.
/// </summary>
/// <remarks>
/// Replaces the ImGui branch combo, which checked the load task's completion state every frame and
/// disabled itself until it finished. Here the drop-down is created disabled, then populated and
/// enabled through a posted UI update when the fetch completes.
/// </remarks>
public class DalamudBranchMetaSettingsEntry : SettingsEntry<string>
{
    private readonly DropDown dropDown = new();
    private List<DalamudBranchMeta.Branch> Branches { get; set; } = [];

    public DalamudBranchMetaSettingsEntry(string name, string description, Func<string> load, Action<string?> save)
        : base(name, description, load, save)
    {
        this.dropDown.Enabled = false;

        Task.Run(async () =>
        {
            var branches = await DalamudBranchMeta.FetchBranchesAsync(Program.HttpClient).ConfigureAwait(false);

            Ui.Post(() =>
            {
                this.Branches = [.. branches];

                foreach (var branch in this.Branches.Where(b => !b.Hidden))
                    this.dropDown.Items.Add(new ListItem { Text = branch.DisplayName, Key = branch.Track });

                var idx = this.Branches.FindIndex(b => b.Track == this.InternalValue as string && !b.Hidden);
                this.dropDown.SelectedIndex = idx;
                this.dropDown.Enabled = true;
            });
        });
    }

    protected override Control CreateInput()
    {
        this.dropDown.SelectedIndexChanged += (_, _) =>
        {
            if (this.dropDown.SelectedIndex < 0)
                return;

            var track = this.dropDown.Items[this.dropDown.SelectedIndex].Key;
            var branch = this.Branches.Find(b => b.Track == track);

            if (branch is null)
                return;

            this.InternalValue = branch.Track;
            this.UpdateState();
        };

        return this.dropDown;
    }

    public override void Save()
    {
        var selectedBranch = this.Branches.Find(b => b.Track == this.InternalValue as string);

        if (selectedBranch is null)
            return;

        Program.Config.DalamudBetaKind = selectedBranch.Track;
        Program.Config.DalamudBetaKey = selectedBranch.Key;
        Program.DalamudUpdater.Run(Program.Config.DalamudBetaKind, Program.Config.DalamudBetaKey);
    }
}
