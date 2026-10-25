namespace TaskbarHwMon;

/// <summary>태스크바(Shell_TrayWnd)의 자식 창으로 붙어 GPU/CPU 두 줄을 그린다.</summary>
sealed class OverlayForm : Form
{
    static readonly Color GpuColor = Color.FromArgb(118, 185, 0), CpuColor = Color.FromArgb(80, 160, 255);
    const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    readonly IntPtr _tray;
    readonly Color _text, _dim;
    Config _config;
    Color _key;
    Font? _font;
    float _scale;
    int[] _colX = [];       // 열 시작 x (숨긴 열은 -1)
    int _width, _rowH;
    Snapshot? _snap;

    public OverlayForm(IntPtr tray, Config config, ContextMenuStrip menu)
    {
        _tray = tray;
        _config = config;
        bool light = Native.SystemUsesLightTheme();
        // 컬러키 투명: 글자 가장자리가 키 색과 섞이므로 키 색은 실제 태스크바 색으로 맞춘다 (SampleKey)
        _key = light ? Color.FromArgb(238, 238, 238) : Color.FromArgb(34, 34, 38);
        _text = light ? Color.FromArgb(20, 20, 20) : Color.FromArgb(240, 240, 240);
        _dim = light ? Color.FromArgb(90, 90, 90) : Color.FromArgb(170, 170, 170);

        // 최상위 폼이면 WinForms가 GWLP_HWNDPARENT로 소유자를 바꿔 부모가 태스크바에서 떨어진다
        TopLevel = false;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = _key;
        ContextMenuStrip = menu;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style = unchecked((int)(Native.WS_CHILD | Native.WS_CLIPSIBLINGS));
            cp.ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            cp.Parent = _tray;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetParent(Handle, _tray);
        Relayout();
        Native.SetLayeredWindowAttributes(Handle, (uint)ColorTranslator.ToWin32(_key), 255, Native.LWA_COLORKEY);
    }

    public void ApplyConfig(Config config)
    {
        _config = config;
        _font?.Dispose();
        _font = null;
        if (IsHandleCreated) Relayout();
        Invalidate();
    }

    public void SetSnapshot(Snapshot s)
    {
        _snap = s;
        Invalidate();
    }

    bool RowVisible(bool gpu) => Metric.All.Any(m => m.Gpu == gpu && _config.IsVisible(m));

    public void Relayout()
    {
        float scale = Native.GetDpiForWindow(_tray) / 96f;
        if (scale <= 0) scale = 1f;
        if (_font == null || Math.Abs(scale - _scale) > 0.01f)
        {
            _scale = scale;
            _font?.Dispose();
            _font = new Font("Segoe UI", _config.FontPx * scale, _config.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            int gap = (int)(10 * scale), x = 0;
            _colX = new int[Metric.ColumnTemplates.Length];
            for (int c = 0; c < _colX.Length; c++)
            {
                bool used = c == 0 || Metric.All.Any(m => m.Column == c && _config.IsVisible(m));
                _colX[c] = used ? x : -1;
                if (used) x += TextRenderer.MeasureText(Metric.ColumnTemplates[c], _font, Size.Empty, TextFlags).Width + gap;
            }
            _width = x - gap;
            _rowH = (int)Math.Ceiling(_font.Height * 1.05f);
        }

        Native.GetClientRect(_tray, out var rc);
        int rows = (RowVisible(true) ? 1 : 0) + (RowVisible(false) ? 1 : 0);
        int w = Math.Max(1, _width), h = Math.Max(1, _rowH * rows);
        int x0 = (int)(_config.OffsetX * _scale);
        // 왼쪽 정렬 태스크바에서는 시작 버튼과 겹치므로 알림 영역 왼쪽에 붙인다
        if (!Native.TaskbarCentered())
        {
            IntPtr notify = Native.FindWindowEx(_tray, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notify != IntPtr.Zero && Native.GetWindowRect(notify, out var nr) && Native.GetWindowRect(_tray, out var tr2))
                x0 = nr.Left - tr2.Left - w - x0;
        }
        int y0 = Math.Max(0, (rc.Bottom - h) / 2);
        Native.SetWindowPos(Handle, Native.HWND_TOP, x0, y0, w, h, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        SampleKey(x0 + w, rc.Bottom / 2);
    }

    /// <summary>창 오른쪽 빈 태스크바 픽셀 몇 개의 중앙값을 키 색으로 쓴다(배경화면·테마 변화 추적).</summary>
    void SampleKey(int clientX, int clientY)
    {
        Native.GetWindowRect(_tray, out var tr);
        var px = new[] { 8, 30, 60 }
            .Select(dx => Native.GetScreenPixel(tr.Left + clientX + (int)(dx * _scale), tr.Top + clientY))
            .Where(c => c.HasValue).Select(c => c!.Value).ToList();
        if (px.Count == 0) return;
        static int Med(IEnumerable<int> v) => v.OrderBy(i => i).ElementAt(v.Count() / 2);
        var c = Color.FromArgb(Med(px.Select(p => (int)p.R)), Med(px.Select(p => (int)p.G)), Med(px.Select(p => (int)p.B)));
        if (Math.Abs(c.R - _key.R) + Math.Abs(c.G - _key.G) + Math.Abs(c.B - _key.B) <= 3) return;
        _key = c;
        BackColor = c;
        Native.SetLayeredWindowAttributes(Handle, (uint)ColorTranslator.ToWin32(_key), 255, Native.LWA_COLORKEY);
        Invalidate();
    }

    Color ToneColor(Tone tone, float? v) => tone switch
    {
        Tone.Dim => _dim,
        Tone.Temp when v >= 90 => Color.FromArgb(255, 80, 80),
        Tone.Temp when v >= 80 => Color.FromArgb(255, 165, 0),
        _ => _text,
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_key);
        var s = _snap;
        if (s == null || _font == null) return;

        int y = 0;
        foreach (bool gpu in new[] { true, false })
        {
            if (!RowVisible(gpu)) continue;
            // GDI ClearType: 배경(키 색 = 태스크바 색)과 섞여 실제 태스크바 위에서 선명하게 보임
            TextRenderer.DrawText(g, gpu ? "GPU" : "CPU", _font, new Point(0, y), gpu ? GpuColor : CpuColor, _key, TextFlags);
            foreach (var m in Metric.All.Where(m => m.Gpu == gpu && _config.IsVisible(m)))
                TextRenderer.DrawText(g, m.Text(s), _font, new Point(_colX[m.Column], y), ToneColor(m.Tone, m.Get(s)), _key, TextFlags);
            y += _rowH;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ContextMenuStrip = null; // 호스트 소유
            _font?.Dispose();
        }
        base.Dispose(disposing);
    }
}
