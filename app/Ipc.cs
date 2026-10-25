namespace TaskbarHwMon;

/// <summary>
/// 일반 권한으로 실행된 두 번째 인스턴스가 관리자 권한 인스턴스에 "설정 열기"를 전달하는 숨은 창.
/// UIPI가 낮은 권한 → 높은 권한 메시지를 막으므로 해당 메시지만 허용한다.
/// </summary>
sealed class Ipc : NativeWindow, IDisposable
{
    const string Title = "TaskbarHwMon.Ipc";
    public const int ShowSettings = 1;
    static readonly uint Msg = Native.RegisterWindowMessage("TaskbarHwMon.Command");
    readonly Action<int> _handler;

    public Ipc(Action<int> handler)
    {
        _handler = handler;
        CreateHandle(new CreateParams { Caption = Title });
        Native.ChangeWindowMessageFilterEx(Handle, Msg, Native.MSGFLT_ALLOW, IntPtr.Zero);
    }

    public static void Send(int command)
    {
        // 관리자 인스턴스가 막 시작된 경우 창이 아직 없을 수 있어 잠깐 기다린다
        for (int i = 0; i < 20; i++)
        {
            IntPtr h = Native.FindWindow(null, Title);
            if (h != IntPtr.Zero) { Native.PostMessage(h, Msg, (IntPtr)command, IntPtr.Zero); return; }
            Thread.Sleep(250);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Msg) { _handler((int)m.WParam); return; }
        base.WndProc(ref m);
    }

    public void Dispose() => DestroyHandle();
}
