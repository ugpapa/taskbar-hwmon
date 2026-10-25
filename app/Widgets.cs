using System.Diagnostics;
using Microsoft.Win32;

namespace TaskbarHwMon;

/// <summary>
/// Windows 11 위젯(날씨 버튼) 제거·복원. 위젯 버튼 자리를 이 앱이 대신 쓰도록 한다.
/// 제거: 정책 AllowNewsAndInterests=0 + Windows Web Experience Pack 제거 + 탐색기 재시작.
/// 복원: 이 앱이 제거한 경우에만 정책을 되돌리고, 패키지가 없으면 Microsoft Store 페이지를 연다.
/// </summary>
static class Widgets
{
    const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Dsh";
    const string PolicyValue = "AllowNewsAndInterests";
    const string MarkerKey = @"SOFTWARE\TaskbarHwMon";
    const string MarkerValue = "ReplacedWidgets";
    const string StoreProductId = "9MSSGKG348SP"; // Windows Web Experience Pack

    public static bool IsDisabled()
    {
        using var k = Registry.LocalMachine.OpenSubKey(PolicyKey);
        return k?.GetValue(PolicyValue) is int v && v == 0;
    }

    static bool ReplacedByUs()
    {
        using var k = Registry.LocalMachine.OpenSubKey(MarkerKey);
        return k?.GetValue(MarkerValue) is int v && v == 1;
    }

    public static void Replace()
    {
        using (var k = Registry.LocalMachine.CreateSubKey(PolicyKey))
            k.SetValue(PolicyValue, 0, RegistryValueKind.DWord);
        using (var k = Registry.LocalMachine.CreateSubKey(MarkerKey))
            k.SetValue(MarkerValue, 1, RegistryValueKind.DWord);

        // Appx 모듈은 Windows PowerShell 5.1에서 바로 동작
        PowerShell("""
            Get-Process Widgets, WidgetService -ErrorAction SilentlyContinue | Stop-Process -Force
            Get-AppxPackage -AllUsers *WebExperience* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
            Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*WebExperience*' |
                Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
            """);
        RestartExplorer();
    }

    /// <param name="onlyIfOurs">제거 시: 이 앱이 위젯을 없앤 경우에만 되돌린다.</param>
    public static void Restore(bool onlyIfOurs)
    {
        if (onlyIfOurs && !ReplacedByUs()) return;

        using (var k = Registry.LocalMachine.OpenSubKey(PolicyKey, writable: true))
            k?.DeleteValue(PolicyValue, throwOnMissingValue: false);
        Registry.LocalMachine.DeleteSubKeyTree(MarkerKey, throwOnMissingSubKey: false);
        RestartExplorer();

        if (PowerShell("if (Get-AppxPackage -AllUsers *WebExperience*) { exit 0 } else { exit 1 }") != 0)
        {
            try { Process.Start(new ProcessStartInfo($"ms-windows-store://pdp/?ProductId={StoreProductId}") { UseShellExecute = true }); }
            catch { }
        }
    }

    static int PowerShell(string script)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script })
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.WaitForExit(120_000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch { return -1; }
    }

    /// <summary>정책을 반영하려면 탐색기를 재시작해야 한다. Winlogon이 일반 권한으로 다시 띄운다.</summary>
    static void RestartExplorer()
    {
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            try { p.Kill(); } catch { }
            p.Dispose();
        }
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(250);
            if (Native.FindWindow("Shell_TrayWnd", null) != IntPtr.Zero) return;
        }
        // 자동 재시작이 꺼진 환경 대비
        try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
    }
}
