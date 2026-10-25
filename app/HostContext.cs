namespace TaskbarHwMon;

/// <summary>센서 읽기·트레이 아이콘·태스크바 오버레이·설정 창을 묶는 앱 본체.</summary>
sealed class HostContext : ApplicationContext
{
    readonly Sensors _sensors = new();
    readonly System.Windows.Forms.Timer _data = new();
    readonly System.Windows.Forms.Timer _watch = new() { Interval = 2000 };
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu = new();
    readonly Ipc _ipc;
    Config _config = Config.Load();
    OverlayForm? _overlay;
    SettingsForm? _settings;
    bool _busy;

    public HostContext(bool openSettings)
    {
        _menu.Items.Add("설정(&S)...", null, (_, _) => ShowSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("종료(&X)", null, (_, _) => ExitThread());
        _menu.Items[0].Font = new Font(_menu.Font, FontStyle.Bold);

        _tray = new NotifyIcon
        {
            Icon = Program.LoadIcon(SystemInformation.SmallIconSize),
            Text = Program.AppName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };

        _ipc = new Ipc(cmd => { if (cmd == Ipc.ShowSettings) ShowSettings(); });

        Apply(_config, save: false);
        _data.Tick += async (_, _) => await Tick();
        _data.Start();
        // 탐색기 재시작 시 태스크바가 새로 생기므로, 창이 사라지면 다시 붙인다
        _watch.Tick += (_, _) => EnsureOverlay();
        _watch.Start();
        EnsureOverlay();
        _ = Tick();

        if (openSettings) ShowSettings();
    }

    public Sensors Sensors => _sensors;

    public void Apply(Config config, bool save)
    {
        _config = config;
        _sensors.CpuFanName = config.CpuFanSensor;
        _data.Interval = Math.Clamp(config.IntervalMs, 250, 10000);
        _overlay?.ApplyConfig(config);
        if (save) config.Save();
    }

    void ShowSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.WindowState = FormWindowState.Normal;
            _settings.Activate();
            return;
        }
        _settings = new SettingsForm(this, _config);
        _settings.Show();
        _settings.Activate();
    }

    void EnsureOverlay()
    {
        IntPtr tray = Native.FindWindow("Shell_TrayWnd", null);
        if (_overlay is { IsDisposed: false, IsHandleCreated: true } && tray != IntPtr.Zero && Native.GetParent(_overlay.Handle) == tray)
        {
            _overlay.Relayout();
            return;
        }
        if (_overlay != null) { try { _overlay.Dispose(); } catch { } _overlay = null; }
        if (tray == IntPtr.Zero) return;
        _overlay = new OverlayForm(tray, _config, _menu);
        _overlay.Show();
    }

    async Task Tick()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var s = await Task.Run(_sensors.Read);
            _overlay?.SetSnapshot(s);
            _tray.Text = Tooltip(s);
        }
        catch { }
        finally { _busy = false; }
    }

    static string Tooltip(Snapshot s)
    {
        static string V(float? v, string fmt, string unit) => v is float f ? f.ToString(fmt) + unit : "-";
        string t = $"GPU {V(s.GpuLoad, "0", "%")} {V(s.GpuTemp, "0", "°C")} {V(s.GpuPower, "0", "W")}\n" +
                   $"CPU {V(s.CpuLoad, "0", "%")} {V(s.CpuTemp, "0", "°C")} {V(s.CpuPower, "0.0", "W")}";
        return t.Length > 127 ? t[..127] : t;
    }

    protected override void ExitThreadCore()
    {
        _data.Stop();
        _watch.Stop();
        _settings?.Close();
        _overlay?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _ipc.Dispose();
        _menu.Dispose();
        _sensors.Dispose();
        base.ExitThreadCore();
    }
}
