using Eto.Drawing;
using Eto.Forms;

namespace XIVLauncher.Core;

/// <summary>
/// A single-button modal dialog, replacing the ImGui modal popup.
/// </summary>
/// <remarks>
/// <see cref="Dialog.ShowModal()"/> pumps the UI loop while the dialog is open, which is what keeps
/// <c>LauncherApp.ShowMessageBlocking</c> blocking a background thread.
/// </remarks>
public class ModalDialog : Dialog
{
    public ModalDialog(string text, string title, string buttonText, Action buttonPressAction)
    {
        this.Title = title;
        this.ClientSize = new Size(500, 300);

        var button = new Button { Text = buttonText, Width = 150 };
        button.Click += (_, _) =>
        {
            this.Close();
            buttonPressAction();
        };

        // HEAD drew the message box in the middle of the launcher window. GTK chooses the position of a
        // new top-level itself, and a Location set before the window is realized is not honoured, so the
        // centre offset is applied when the dialog is shown.
        void CenterOverMainForm()
        {
            if (Application.Instance.MainForm is not Window owner)
                return;

            this.Location = new Point(
                owner.Location.X + (owner.ClientSize.Width - this.ClientSize.Width) / 2,
                owner.Location.Y + (owner.ClientSize.Height - this.ClientSize.Height) / 2);
        }

        this.Shown += (_, _) => CenterOverMainForm();

        this.Content = new StackLayout
        {
            Padding = new Padding(16),
            Items =
            {
                new StackLayoutItem(new Label { Text = text, Wrap = WrapMode.Character }, expand: true),
                new StackLayoutItem(button, HorizontalAlignment.Center),
            },
        };
    }
}
