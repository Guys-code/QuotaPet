namespace QuotaPet.App;

internal sealed class DeepSeekSettingsForm : Form
{
    private readonly TextBox _key = new() { UseSystemPasswordChar = true, Dock = DockStyle.Top };

    public string? ApiKey { get; private set; }
    public bool RemoveRequested { get; private set; }

    public DeepSeekSettingsForm(bool configured)
    {
        Text = "设置 DeepSeek API";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(430, 190);
        Font = new Font("Microsoft YaHei UI", 9F);

        var label = new Label
        {
            Text = configured
                ? "已安全保存一个 API Key。输入新 Key 可替换；留空不会改变。"
                : "输入 DeepSeek API Key。它将保存在 Windows 凭据管理器中。",
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(0, 8, 0, 6)
        };
        var hint = new Label
        {
            Text = "桌宠只调用官方 /user/balance 接口，每 5 分钟刷新一次。",
            Dock = DockStyle.Top,
            Height = 38,
            ForeColor = Color.SlateGray,
            Padding = new Padding(0, 8, 0, 0)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        var save = new Button { Text = "保存", DialogResult = DialogResult.OK, AutoSize = true };
        save.Click += (_, _) =>
        {
            var value = _key.Text.Trim();
            if (value.Length == 0)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            ApiKey = value;
        };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        if (configured)
        {
            var remove = new Button { Text = "移除密钥", AutoSize = true };
            remove.Click += (_, _) =>
            {
                RemoveRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(remove);
        }

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 4, 18, 0) };
        content.Controls.Add(hint);
        content.Controls.Add(_key);
        content.Controls.Add(label);
        Controls.Add(content);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
