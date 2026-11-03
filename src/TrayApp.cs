using System.Net.Sockets;

namespace LanAudio.Sender;

/// <summary>通知領域（タスクバー右下）に常駐し、メニューから送信を操作する</summary>
sealed class TrayApp : ApplicationContext
{
    const string AppName = "LanAudio 送信";

    readonly NotifyIcon tray;
    readonly ToolStripMenuItem statusItem, detailItem, toggleItem, startupItem;
    readonly System.Windows.Forms.Timer statusTimer, restartTimer;
    readonly Icon iconOff, iconIdle, iconActive;
    readonly SynchronizationContext ui;
    readonly DefaultDeviceWatcher deviceWatcher = new();
    AppSettings settings;
    SenderEngine? engine;
    SettingsForm? settingsForm;

    public TrayApp(string[] args)
    {
        // 互換: AudioSender.exe <受信側IP> [ポート]
        settings = AppSettings.Load();
        if (args.Length >= 1) settings.ServerHost = args[0];
        if (args.Length >= 2 && int.TryParse(args[1], out int p)) settings.Port = p;
        if (args.Length > 0) settings.Save();

        int size = SystemInformation.SmallIconSize.Width;
        iconOff = ToIcon(IconArt.Render(size, IconArt.Disabled, false));
        iconIdle = ToIcon(IconArt.Render(size, IconArt.Accent, false));
        iconActive = ToIcon(IconArt.Render(size, IconArt.Accent, true));

        statusItem = new ToolStripMenuItem { Enabled = false };
        detailItem = new ToolStripMenuItem { Enabled = false };
        toggleItem = new ToolStripMenuItem("", null, (_, _) => Toggle()) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        startupItem = new ToolStripMenuItem("Windows 起動時に起動", null, (_, _) => Startup.SetEnabled(!Startup.IsEnabled));

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            statusItem, detailItem,
            new ToolStripSeparator(),
            toggleItem,
            new ToolStripMenuItem("設定...", null, (_, _) => ShowSettings()),
            new ToolStripSeparator(),
            startupItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("終了", null, (_, _) => ExitThread()),
        });
        menu.Opening += (_, _) =>
        {
            startupItem.Checked = Startup.IsEnabled;
            UpdateStatus();
        };
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        tray = new NotifyIcon { ContextMenuStrip = menu, Icon = iconOff, Text = AppName, Visible = true };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };

        statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        statusTimer.Tick += (_, _) => UpdateStatus();
        statusTimer.Start();

        // 既定の再生デバイスが変わった・取り込みが止まったときは作り直す（少し待ってから）
        restartTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        restartTimer.Tick += (_, _) => { restartTimer.Stop(); if (engine != null) Start(); };
        deviceWatcher.Changed += () => ui.Post(_ => ScheduleRestart(), null);

        Startup.RefreshPath();
        if (settings.ServerHost.Length == 0)
            ui.Post(_ => ShowSettings(), null);   // 初回は送信先の入力から
        else if (settings.AutoStart || args.Length > 0)
            Start();
        UpdateStatus();
    }

    void Toggle()
    {
        if (engine != null) Stop();
        else if (settings.ServerHost.Length == 0) ShowSettings();
        else Start();
    }

    void Start()
    {
        Stop();
        try
        {
            engine = new SenderEngine(settings.ServerHost, settings.Port);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            Notify($"送信を開始できませんでした。\n{ex.Message}", ToolTipIcon.Error);
            UpdateStatus();
            return;
        }
        var started = engine;
        engine.Stopped += _ => ui.Post(_ => { if (engine == started) ScheduleRestart(); }, null);
        UpdateStatus();
    }

    void Stop()
    {
        engine?.Dispose();
        engine = null;
        UpdateStatus();
    }

    void ScheduleRestart()
    {
        restartTimer.Stop();
        restartTimer.Start();
    }

    void UpdateStatus()
    {
        string status, detail = "";
        Icon icon;
        if (engine == null)
        {
            status = "停止中";
            icon = iconOff;
        }
        else if (engine.HasRecentError)
        {
            status = $"受信側が応答しません → {engine.Destination}";
            detail = "受信側のアプリが起動しているか確認してください";
            icon = iconIdle;
        }
        else if (engine.IsSending)
        {
            status = $"送信中 → {engine.Destination}";
            detail = $"{engine.Format} / {engine.SentPackets} パケット";
            icon = iconActive;
        }
        else
        {
            status = $"待機中（再生音なし） → {engine.Destination}";
            icon = iconIdle;
        }

        statusItem.Text = status;
        detailItem.Text = detail;
        detailItem.Visible = detail.Length > 0;
        toggleItem.Text = engine == null ? "送信を開始" : "送信を停止";
        string tip = $"{AppName}\n{status}";
        tray.Text = tip.Length > 127 ? tip[..127] : tip;
        if (tray.Icon != icon) tray.Icon = icon;
    }

    void ShowSettings()
    {
        if (settingsForm != null)
        {
            settingsForm.Activate();
            return;
        }
        using (settingsForm = new SettingsForm(settings, Startup.IsEnabled))
        {
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                bool firstTime = settings.ServerHost.Length == 0;
                settings = settingsForm.Result;
                settings.Save();
                Startup.SetEnabled(settingsForm.StartupEnabled);
                if (engine != null || firstTime) Start();   // 新しい設定で（再）開始
            }
        }
        settingsForm = null;
    }

    void Notify(string message, ToolTipIcon icon) => tray.ShowBalloonTip(5000, AppName, message, icon);

    static Icon ToIcon(Bitmap bmp)
    {
        using (bmp) return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        statusTimer.Stop();
        restartTimer.Stop();
        deviceWatcher.Dispose();
        engine?.Dispose();
        engine = null;
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
