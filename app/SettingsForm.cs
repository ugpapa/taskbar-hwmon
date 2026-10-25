namespace TaskbarHwMon;

/// <summary>설정 창. 바꾸는 즉시 태스크바에 미리 보이고, 확인 시 저장·취소 시 되돌린다.</summary>
sealed class SettingsForm : Form
{
    readonly HostContext _host;
    readonly Config _original, _work;
    readonly Dictionary<string, CheckBox> _checks = [];
    readonly ComboBox _fan = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly NumericUpDown _offset = new() { Minimum = 0, Maximum = 3000, Width = 80 };
    readonly NumericUpDown _fontPx = new() { Minimum = 9, Maximum = 24, Width = 80 };
    readonly CheckBox _bold = new() { Text = "굵게", AutoSize = true };
    readonly ComboBox _interval = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly CheckBox _autoStart = new() { Text = "Windows 시작 시 자동 실행", AutoSize = true };
    static readonly int[] Intervals = [500, 1000, 2000, 5000];
    bool _loading = true, _saved;

    // 표의 행: (이름, GPU 항목, CPU 항목) — 같은 열에 놓이는 항목끼리 짝지음
    static readonly (string Label, string Gpu, string Cpu)[] Rows =
    [
        ("사용률", "GpuLoad", "CpuLoad"),
        ("온도", "GpuTemp", "CpuTemp"),
        ("전력", "GpuPower", "CpuPower"),
        ("클럭", "GpuClock", "CpuClock"),
        ("전압", "GpuVolt", "CpuVolt"),
        ("메모리 클럭", "VramClock", "DramClock"),
        ("팬 속도", "GpuFan", "CpuFan"),
    ];

    public SettingsForm(HostContext host, Config config)
    {
        _host = host;
        _original = config.Clone();
        _work = config.Clone();

        Text = $"{Program.AppName} 설정";
        Icon = Program.LoadIcon(new Size(32, 32));
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        root.Controls.Add(Group("표시 항목", BuildItems()));
        root.Controls.Add(Group("모양", BuildLook()));
        root.Controls.Add(Group("시작", _autoStart));
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

    Control BuildItems()
    {
        var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true };
        t.Controls.Add(Lbl(""), 0, 0);
        t.Controls.Add(Lbl("GPU", true), 1, 0);
        t.Controls.Add(Lbl("CPU", true), 2, 0);
        for (int r = 0; r < Rows.Length; r++)
        {
            t.Controls.Add(Lbl(Rows[r].Label), 0, r + 1);
            t.Controls.Add(Check(Rows[r].Gpu), 1, r + 1);
            t.Controls.Add(Check(Rows[r].Cpu), 2, r + 1);
        }
        t.Controls.Add(Lbl("CPU 쿨러 팬"), 0, Rows.Length + 1);
        t.Controls.Add(_fan, 1, Rows.Length + 1);
        t.SetColumnSpan(_fan, 2);
        _fan.SelectedIndexChanged += (_, _) => Changed();
        return t;
    }

    CheckBox Check(string id)
    {
        var m = Metric.All.First(m => m.Id == id);
        var c = new CheckBox { AutoSize = true, Margin = new Padding(3, 5, 24, 3), Tag = m };
        c.CheckedChanged += (_, _) => Changed();
        _checks[id] = c;
        return c;
    }

    Control BuildLook()
    {
        var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true };
        t.Controls.Add(Lbl("왼쪽 여백"), 0, 0);
        t.Controls.Add(_offset, 1, 0);
        t.Controls.Add(Lbl("px"), 2, 0);
        t.Controls.Add(Lbl("글자 크기"), 0, 1);
        t.Controls.Add(_fontPx, 1, 1);
        t.Controls.Add(_bold, 2, 1);
        t.Controls.Add(Lbl("갱신 간격"), 0, 2);
        t.Controls.Add(_interval, 1, 2);
        t.SetColumnSpan(_interval, 2);
        _interval.Items.AddRange(Intervals.Select(ms => (object)$"{ms / 1000.0:0.#}초").ToArray());
        _offset.ValueChanged += (_, _) => Changed();
        _fontPx.ValueChanged += (_, _) => Changed();
        _bold.CheckedChanged += (_, _) => Changed();
        _interval.SelectedIndexChanged += (_, _) => Changed();
        return t;
    }

    Control BuildButtons()
    {
        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(88, 0) };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(88, 0) };
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
        _autoStart.Checked = AutoStart.IsEnabledFor(Program.ExePath);
    }

    sealed record FanItem(string Name, float? Rpm)
    {
        public override string ToString() => Rpm is float r ? $"{Name}  (현재 {r:0} RPM)" : Name;
    }

    void Changed()
    {
        if (_loading) return;
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
