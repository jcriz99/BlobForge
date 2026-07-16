using System.Media;

namespace BlobForge;

internal sealed class TestGateForm : Form
{
    private TestGateForm(string label, Color color, bool confirmation)
    {
        Text = "BlobForge";
        BackColor = color;
        ClientSize = new Size(300, 135);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;

        Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 75,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 28, 35)
        });

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = confirmation ? new Padding(57, 8, 0, 0) : new Padding(106, 8, 0, 0)
        };
        if (confirmation)
        {
            buttons.Controls.Add(MakeButton("START", DialogResult.OK));
            buttons.Controls.Add(MakeButton("CANCEL", DialogResult.Cancel));
            CancelButton = (Button)buttons.Controls[1];
            AcceptButton = (Button)buttons.Controls[0];
        }
        else
        {
            buttons.Controls.Add(MakeButton("OK", DialogResult.OK));
            AcceptButton = (Button)buttons.Controls[0];
        }
        Controls.Add(buttons);
    }

    public static bool ConfirmStart()
    {
        SystemSounds.Exclamation.Play();
        using var form = new TestGateForm("START TEST?", Color.FromArgb(255, 214, 64), true);
        return form.ShowDialog() == DialogResult.OK;
    }

    public static void ShowDone()
    {
        SystemSounds.Asterisk.Play();
        using var form = new TestGateForm("DONE", Color.FromArgb(91, 214, 116), false);
        form.ShowDialog();
    }

    private static Button MakeButton(string text, DialogResult result)
        => new()
        {
            Text = text,
            DialogResult = result,
            Size = new Size(86, 34),
            Margin = new Padding(3, 0, 3, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
}
