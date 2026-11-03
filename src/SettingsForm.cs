namespace LanAudio.Sender;

sealed class SettingsForm : Form
{
    readonly TextBox host = new() { Width = 200 };
    readonly NumericUpDown port = new() { Minimum = 1024, Maximum = 65535, Width = 120 };
    readonly CheckBox autoStart = new() { Text = "アプリ起動時に送信を開始する", AutoSize = true };
    readonly CheckBox startup = new() { Text = "Windows 起動時にアプリを起動する", AutoSize = true };

    public SettingsForm(AppSettings settings, bool startupEnabled)
    {
        Text = "LanAudio 送信 - 設定";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        host.Text = settings.ServerHost;
        port.Value = Math.Clamp(settings.Port, (int)port.Minimum, (int)port.Maximum);
        autoStart.Checked = settings.AutoStart;
        startup.Checked = startupEnabled;

        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        AddRow(table, "受信側PCの IP アドレス", host);
        AddWide(table, new Label
        {
            Text = "受信側（デスクトップPC）のトレイメニュー「このPCのIPアドレス」で確認できます",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 8),
        });
        AddRow(table, "ポート (UDP)", port);
        AddWide(table, autoStart);
        AddWide(table, startup);

        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += (_, _) =>
        {
            if (host.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "受信側PCの IP アドレスを入力してください。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                host.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
        };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        AddWide(table, buttons);

        Controls.Add(table);
        AcceptButton = ok;
        CancelButton = cancel;
        // トレイから開いたときに他のウィンドウの裏に隠れないようにする
        Shown += (_, _) => { TopMost = true; Activate(); TopMost = false; };
    }

    public AppSettings Result => new()
    {
        ServerHost = host.Text.Trim(),
        Port = (int)port.Value,
        AutoStart = autoStart.Checked,
    };

    public bool StartupEnabled => startup.Checked;

    static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) });
        table.Controls.Add(control);
    }

    static void AddWide(TableLayoutPanel table, Control control)
    {
        table.Controls.Add(control);
        table.SetColumnSpan(control, 2);
    }
}
