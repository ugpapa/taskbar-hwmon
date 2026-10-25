using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace TaskbarHwMon;

static class Program
{
    public const string AppName = "Taskbar HW Monitor";
    const string MutexName = @"Local\TaskbarHwMon";

    public static string ExePath => Environment.ProcessPath!;

    public static bool IsElevated =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    [STAThread]
    static int Main(string[] args)
    {
        // 설치 프로그램에서 호출 (관리자 권한으로 실행됨)
        if (args.Contains("--register-autostart")) return AutoStart.Enable(ExePath) ? 0 : 1;
        if (args.Contains("--unregister-autostart")) { AutoStart.Disable(); return 0; }

        bool openSettings = args.Contains("--settings");

        if (!IsElevated)
        {
            // 이미 실행 중이면 설정 창만 띄우고, 아니면 작업 스케줄러(UAC 없음) → 안 되면 UAC로 승격
            if (InstanceExists()) { Ipc.Send(Ipc.ShowSettings); return 0; }
            if (AutoStart.IsEnabledFor(ExePath) && AutoStart.Run()) return 0;
            try
            {
                Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true, Verb = "runas", Arguments = openSettings ? "--settings" : "" });
            }
            catch (Win32Exception) { } // UAC 취소
            return 0;
        }

        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created) { Ipc.Send(Ipc.ShowSettings); return 0; }

        ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001
        Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001
        Application.Run(new HostContext(openSettings));
        return 0;
    }

    static bool InstanceExists()
    {
        try { return Mutex.TryOpenExisting(MutexName, out var m) && DisposeTrue(m); }
        catch (UnauthorizedAccessException) { return true; } // 관리자 인스턴스가 만든 뮤텍스
    }

    static bool DisposeTrue(Mutex m) { m.Dispose(); return true; }

    public static Icon LoadIcon(Size size)
    {
        using var s = typeof(Program).Assembly.GetManifestResourceStream("app.ico")!;
        return new Icon(s, size);
    }
}
