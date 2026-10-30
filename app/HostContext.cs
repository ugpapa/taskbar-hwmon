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
    readonly Dictionary<IntPtr, OverlayForm> _overlays = [];   // 태스크바 창 → 오버레이
    SettingsForm? _settings;
    bool _busy;

    public HostContext(bool openSettings)
    {
        L.Use(_config.EffectiveLanguage);
        _menuSettings = new ToolStripMenuItem("", null, (_, _) => ShowSettings()) { Font = new Font(_menu.Font, FontStyle.Bold) };
        _menuCompact = new ToolStripMenuItem("", null, (_, _) => ToggleCompactAll());
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
        _watch.Tick += (_, _) => EnsureOverlays();
        _watch.Start();
        EnsureOverlays();
        _ = Tick();

        if (openSettings) ShowSettings();
    }

    public Sensors Sensors => _sensors;

    public void Apply(Config config, bool save)
    {
        _config = config;
        _sensors.CpuFanName = config.CpuFanSensor;
        _data.Interval = Math.Clamp(config.IntervalMs, 250, 10000);
        foreach (var o in _overlays.Values) o.ApplyConfig(config);
        if (_overlays.Count > 0) EnsureOverlays(); // 보조 모니터 표시 켜고 끄기 즉시 반영
        if (save) config.Save();
        L.Use(config.EffectiveLanguage);
        _menuSettings.Text = L.T("MenuSettings");
        _menuCompact.Text = L.T("MenuCompact");
        _menuCompact.Checked = config.Compact;
        _menuExit.Text = L.T("MenuExit");
    }

    /// <summary>태스크바 표시 클릭: 클릭한 태스크바(주 / 보조)만 전체 ↔ 간편 전환 (저장됨).</summary>
    void ToggleCompact(bool secondary)
    {
        var c = _config.Clone();
        if (secondary) c.CompactSecondary = !c.CompactSecondary;
        else c.Compact = !c.Compact;
        Apply(c, save: true);
    }

    /// <summary>트레이 메뉴: 모든 태스크바를 주 모니터 기준 반대 상태로 맞춘다.</summary>
    void ToggleCompactAll()
    {
        var c = _config.Clone();
        c.Compact = c.CompactSecondary = !c.Compact;
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

    /// <summary>주 태스크바 + (설정 시) 보조 모니터 태스크바마다 오버레이 하나씩. 모니터 연결·해제도 여기서 따라간다.</summary>
    void EnsureOverlays()
    {
        var trays = new List<IntPtr>();
        IntPtr main = Native.FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) trays.Add(main);
        int secondaryFrom = trays.Count;
        if (_config.AllMonitors)
            for (IntPtr h = IntPtr.Zero; (h = Native.FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero;)
                trays.Add(h);

        foreach (var (tray, o) in _overlays.ToList())
        {
            if (trays.Contains(tray) && o is { IsDisposed: false, IsHandleCreated: true } && Native.GetParent(o.Handle) == tray) continue;
            try { o.Dispose(); } catch { }
            _overlays.Remove(tray);
        }
        for (int i = 0; i < trays.Count; i++)
        {
            IntPtr tray = trays[i];
            if (_overlays.TryGetValue(tray, out var o)) { o.Relayout(); continue; }
            o = new OverlayForm(tray, i >= secondaryFrom, _config, _menu, ToggleCompact);
            _overlays[tray] = o;
            o.Show();
        }
    }

    async Task Tick()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var s = await Task.Run(_sensors.Read);
            foreach (var o in _overlays.Values) o.SetSnapshot(s);
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
        foreach (var o in _overlays.Values) o.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _ipc.Dispose();
        _menu.Dispose();
        _sensors.Dispose();
        base.ExitThreadCore();
    }
}
