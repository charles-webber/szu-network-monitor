using System.Drawing;
using System.Windows.Forms;

namespace SZUNetworkMonitor;

internal sealed class SetupForm : Form
{
    private readonly TextBox _usernameTextBox = new() { Width = 280 };
    private readonly TextBox _passwordTextBox = new() { Width = 280, UseSystemPasswordChar = true };
    private readonly CheckBox _startupCheckBox = new()
    {
        AutoSize = true,
        Text = "\u767b\u5f55 Windows \u540e\u81ea\u52a8\u542f\u52a8",
        Checked = true
    };
    private readonly Button _saveButton = new()
    {
        Text = "\u4fdd\u5b58\u5e76\u542f\u52a8\u76d1\u63a7",
        AutoSize = true
    };

    public event Action<string, string, bool>? SettingsSaved;

    public SetupForm(AppSettings? settings, string currentPassword)
    {
        Text = "\u6df1\u5733\u5927\u5b66\u6821\u56ed\u7f51\u76d1\u63a7";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(450, 270);

        _usernameTextBox.Text = settings?.Username ?? string.Empty;
        _passwordTextBox.Text = currentPassword;
        _startupCheckBox.Checked = settings?.StartWithWindows ?? true;

        var heading = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "\u9996\u6b21\u8bbe\u7f6e",
            Margin = new Padding(0, 0, 0, 8)
        };
        var description = new Label
        {
            AutoSize = false,
            Width = 390,
            Height = 42,
            Text = "\u8f93\u5165\u6821\u56ed\u7f51\u8d26\u53f7\u5bc6\u7801\u540e\uff0c\u7a0b\u5e8f\u4f1a\u6bcf 1 \u5206\u949f\u68c0\u6d4b\u5916\u7f51\u3002\u4efb\u610f Ping \u4e22\u5305\u4f1a\u81ea\u52a8\u91cd\u8fde\u3002",
            Margin = new Padding(0, 0, 0, 14)
        };

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 14)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.Controls.Add(new Label { AutoSize = true, Text = "\u8d26\u53f7\uff1a", Anchor = AnchorStyles.Left }, 0, 0);
        grid.Controls.Add(_usernameTextBox, 1, 0);
        grid.Controls.Add(new Label { AutoSize = true, Text = "\u5bc6\u7801\uff1a", Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(_passwordTextBox, 1, 1);

        _saveButton.Click += SaveButtonClick;
        AcceptButton = _saveButton;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 16, 0, 0)
        };
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(new Button { Text = "\u53d6\u6d88", AutoSize = true, DialogResult = DialogResult.Cancel });
        CancelButton = (Button)buttons.Controls[1];

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        layout.Controls.Add(heading);
        layout.Controls.Add(description);
        layout.Controls.Add(grid);
        layout.Controls.Add(_startupCheckBox);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
    }

    private void SaveButtonClick(object? sender, EventArgs eventArgs)
    {
        var username = _usernameTextBox.Text.Trim();
        var password = _passwordTextBox.Text;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(this, "\u8bf7\u586b\u5199\u6821\u56ed\u7f51\u8d26\u53f7\u548c\u5bc6\u7801\u3002", "\u4fe1\u606f\u4e0d\u5b8c\u6574", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SettingsSaved?.Invoke(username, password, _startupCheckBox.Checked);
        DialogResult = DialogResult.OK;
        Close();
    }
}
