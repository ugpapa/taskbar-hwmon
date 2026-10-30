namespace TaskbarHwMon;

/// <summary>설정 창. 바꾸는 즉시 태스크바에 미리 보이고, 확인 시 저장·취소 시 되돌린다.</summary>
sealed class SettingsForm : Form
{
    readonly HostContext _host;
    readonly Config _original, _work;
    readonly Dictionary<string, CheckBox> _checks = [];
    TableLayoutPanel _items = null!;
    readonly ComboBox _fan = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly CheckBox _clickToggle = new() { AutoSize = true };
    readonly CheckBox _allMonitors = new() { AutoSize = true };
    readonly NumericUpDown _offset = new() { Minimum = 0, Maximum = 3000, Width = 80 };
    readonly NumericUpDown _fontPx = new() { Minimum = 9, Maximum = 24, Width = 80 };
    readonly CheckBox _bold = new() { AutoSize = true };
    readonly ComboBox _interval = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly CheckBox _autoStart = new() { AutoSize = true };
    static readonly int[] Intervals = [500, 1000, 2000, 5000];
    bool _loading = true, _saved;

    // 표의 행: (문자열 키, GPU 항목, CPU 항목) — 같은 열에 놓이는 항목끼리 짝지음
    static readonly (string Key, string Gpu, string Cpu)[] Rows =
    [
        ("Usage", "GpuLoad", "CpuLoad"),
        ("Temp", "GpuTemp", "CpuTemp"),
        ("Power", "GpuPower", "CpuPower"),
        ("Clock", "GpuClock", "CpuClock"),
        ("Voltage", "GpuVolt", "CpuVolt"),
        ("MemClock", "VramClock", "DramClock"),
        ("Fan", "GpuFan", "CpuFan"),
    ];

    public SettingsForm(HostContext host, Config config)
    {
        _host = host;
        _original = config.Clone();
        _work = config.Clone();

        Text = L.T("SettingsTitle", Program.AppName);
        Icon = Program.LoadIcon(new Size(32, 32));
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _clickToggle.Text = L.T("ClickToggle");
        _allMonitors.Text = L.T("AllMonitors");
        _bold.Text = L.T("Bold");
        _autoStart.Text = L.T("AutoStart");

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        root.Controls.Add(Group(L.T("DisplayMode"), BuildMode()));
        root.Controls.Add(Group(L.T("Items"), BuildItems()));
        root.Controls.Add(Group(L.T("Look"), BuildLook()));
        root.Controls.Add(Group(L.T("Start"), _autoStart));
        root.Controls.Add(BuildButtons());
        Controls.Add(root);

        LoadValues();
        _loading = false;
    }

    static GroupBox Group(string title, Control content)
    {
        var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        content.Dock = DockStyle.Fill;
        g.Controls.Add(content);
        return g;
    }

    static Label Lbl(string text, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 3),
        Font = bold ? new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold) : null,
    };

    Control BuildMode()
    {
        var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        _mode.Items.AddRange([L.T("ModeFull"), L.T("ModeCompact")]);
        t.Controls.Add(_mode);
        t.Controls.Add(_clickToggle);
        t.Controls.Add(_allMonitors);
        // 설정 창의 표시 모드는 모든 태스크바에 적용 (클릭 전환만 태스크바별)
        _mode.SelectedIndexChanged += (_, _) => { if (!_loading) _work.CompactSecondary = _mode.SelectedIndex == 1; Changed(); };
        _clickToggle.CheckedChanged += (_, _) => Changed();
        _allMonitors.CheckedChanged += (_, _) => Changed();
        return t;
    }

    Control BuildItems()
    {
        var t = _items = new TableLayoutPanel { ColumnCount = 3, AutoSize = true };
        t.Controls.Add(Lbl(""), 0, 0);
        t.Controls.Add(Lbl("GPU", true), 1, 0);
        t.Controls.Add(Lbl("CPU", true), 2, 0);
        for (int r = 0; r < Rows.Length; r++)
        {
            t.Controls.Add(Lbl(L.T(Rows[r].Key)), 0, r + 1);
            t.Controls.Add(Check(Rows[r].Gpu), 1, r + 1);
            t.Controls.Add(Check(Rows[r].Cpu), 2, r + 1);
        }
        // 팬 선택은 간편 모드(CPU 팬 RPM 표시)에서도 써야 하므로 비활성화되는 표 밖에 둔다
        var fanRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        fanRow.Controls.Add(Lbl(L.T("CpuCoolerFan")));
        fanRow.Controls.Add(_fan);
        _fan.SelectedIndexChanged += (_, _) => Changed();
        var outer = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        outer.Controls.Add(t);
        outer.Controls.Add(fanRow);
        return outer;
    }

    CheckBox Check(string id)
    {
        var c = new CheckBox { AutoSize = true, Margin = new Padding(3, 5, 24, 3) };
        c.CheckedChanged += (_, _) => Changed();
        _checks[id] = c;
        return c;
    }

    Control BuildLook()
    {
        var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true };
        t.Controls.Add(Lbl(L.T("LeftMargin")), 0, 0);
        t.Controls.Add(_offset, 1, 0);
        t.Controls.Add(Lbl("px"), 2, 0);
        t.Controls.Add(Lbl(L.T("FontSize")), 0, 1);
        t.Controls.Add(_fontPx, 1, 1);
        t.Controls.Add(_bold, 2, 1);
        t.Controls.Add(Lbl(L.T("Interval")), 0, 2);
        t.Controls.Add(_interval, 1, 2);
        t.SetColumnSpan(_interval, 2);
        t.Controls.Add(Lbl(L.T("Language")), 0, 3);
        t.Controls.Add(_language, 1, 3);
        t.SetColumnSpan(_language, 2);
        _interval.Items.AddRange(Intervals.Select(ms => (object)L.T("Seconds", (ms / 1000.0).ToString("0.#"))).ToArray());
        _language.Items.Add(L.T("LangAuto"));
        _language.Items.AddRange(L.Languages.Select(l => (object)l.Name).ToArray());
        _offset.ValueChanged += (_, _) => Changed();
        _fontPx.ValueChanged += (_, _) => Changed();
        _bold.CheckedChanged += (_, _) => Changed();
        _interval.SelectedIndexChanged += (_, _) => Changed();
        return t;
    }

    Control BuildButtons()
    {
        var ok = new Button { Text = L.T("OK"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(88, 0) };
        var cancel = new Button { Text = L.T("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(88, 0) };
        ok.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        AcceptButton = ok;
        CancelButton = cancel;
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        p.Controls.Add(cancel);
        p.Controls.Add(ok);
        return p;
    }

    void LoadValues()
    {
        _mode.SelectedIndex = _work.Compact ? 1 : 0;
        _clickToggle.Checked = _work.ClickToggle;
        _allMonitors.Checked = _work.AllMonitors;
        _items.Enabled = !_work.Compact;
        foreach (var (id, c) in _checks) c.Checked = !_work.Hidden.Contains(id);

        foreach (var (name, rpm) in _host.Sensors.MotherboardFans())
            _fan.Items.Add(new FanItem(name, rpm));
        if (_fan.Items.Count == 0) _fan.Items.Add(new FanItem(_work.CpuFanSensor, null));
        _fan.SelectedItem = _fan.Items.Cast<FanItem>().FirstOrDefault(f => f.Name == _work.CpuFanSensor) ?? _fan.Items[0];

        _offset.Value = Math.Clamp(_work.OffsetX, (int)_offset.Minimum, (int)_offset.Maximum);
        _fontPx.Value = (decimal)Math.Clamp(_work.FontPx, (float)_fontPx.Minimum, (float)_fontPx.Maximum);
        _bold.Checked = _work.Bold;
        int idx = Array.IndexOf(Intervals, _work.IntervalMs);
        _interval.SelectedIndex = idx >= 0 ? idx : 1;
        _language.SelectedIndex = Array.FindIndex(L.Languages, l => l.Code == _work.EffectiveLanguage) + 1; // 없으면 0 = 자동
        _autoStart.Checked = AutoStart.IsEnabledFor(Program.ExePath);
    }

    sealed record FanItem(string Name, float? Rpm)
    {
        public override string ToString() => Rpm is float r ? L.T("FanCurrent", Name, r.ToString("0")) : Name;
    }

    void Changed()
    {
        if (_loading) return;
        _work.Compact = _mode.SelectedIndex == 1;
        _work.ClickToggle = _clickToggle.Checked;
        _work.AllMonitors = _allMonitors.Checked;
        _items.Enabled = !_work.Compact;
        _work.Hidden = _checks.Where(kv => !kv.Value.Checked).Select(kv => kv.Key).ToHashSet();
        _work.CpuFanSensor = (_fan.SelectedItem as FanItem)?.Name ?? _work.CpuFanSensor;
        _work.OffsetX = (int)_offset.Value;
        _work.FontPx = (float)_fontPx.Value;
        _work.Bold = _bold.Checked;
        _work.IntervalMs = Intervals[Math.Max(0, _interval.SelectedIndex)];
        _host.Apply(_work.Clone(), save: false); // 미리 보기
    }

    void Save()
    {
        _saved = true;
        _work.Language = _language.SelectedIndex <= 0 ? "auto" : L.Languages[_language.SelectedIndex - 1].Code;
        _host.Apply(_work.Clone(), save: true);
        bool enabled = AutoStart.IsEnabledFor(Program.ExePath);
        if (_autoStart.Checked && !enabled) AutoStart.Enable(Program.ExePath);
        else if (!_autoStart.Checked && enabled) AutoStart.Disable();
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (!_saved) _host.Apply(_original, save: false);
        base.OnFormClosed(e);
    }
}
