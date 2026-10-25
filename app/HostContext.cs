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
    readonly ToolStripMenuItem _menuSettings, _menuCompact, _menuExit;
    Config _config = Config.Load();
    OverlayForm? _overlay;
    SettingsForm? _settings;
    bool _busy;

    public HostContext(bool openSettings)
    {
        L.Use(_config.EffectiveLanguage);
        _menuSettings = new ToolStripMenuItem("", null, (_, _) => ShowSettings()) { Font = new Font(_menu.Font, FontStyle.Bold) };
        _menuCompact = new ToolStripMenuItem("", null, (_, _) => ToggleCompact());
        _menuExit = new ToolStripMenuItem("", null, (_, _) => ExitThread());
        _menu.Items.AddRange([_menuSettings, _menuCompact, new ToolStripSeparator(), _menuExit]);

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
        L.Use(config.EffectiveLanguage);
        _menuSettings.Text = L.T("MenuSettings");
        _menuCompact.Text = L.T("MenuCompact");
        _menuCompact.Checked = config.Compact;
        _menuExit.Text = L.T("MenuExit");
    }

    /// <summary>태스크바 표시 클릭·트레이 메뉴: 전체 ↔ 간편 전환 (저장됨).</summary>
    void ToggleCompact()
    {
        var c = _config.Clone();
        c.Compact = !c.Compact;
        Apply(c, save: true);
    }

    void ShowSettings()
    {
        if (_settings is not { IsDisposed: false })
        {
            _settings = new SettingsForm(this, _config);
            _settings.Show();
        }
        _settings.WindowState = FormWindowState.Normal;
        // 전경 잠금으로 Activate가 무시되어도 다른 창 위에 보이도록 잠깐 최상위로 올린다
        _settings.TopMost = true;
        _settings.Activate();
        _settings.TopMost = false;
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
        _overlay = new OverlayForm(tray, _config, _menu, ToggleCompact);
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
