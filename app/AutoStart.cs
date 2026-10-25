using System.Diagnostics;
using System.Security;
using System.Security.Principal;

namespace TaskbarHwMon;

/// <summary>로그온 시 관리자 권한으로 UAC 없이 실행되도록 작업 스케줄러 작업을 관리.</summary>
static class AutoStart
{
    const string TaskName = "TaskbarHwMon";

    static (int code, string output) Schtasks(params string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output);
    }

    public static bool IsEnabledFor(string exe)
    {
        try
        {
            var (code, xml) = Schtasks("/query", "/tn", TaskName, "/xml");
            return code == 0 && xml.Contains(exe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool Run()
    {
        try { return Schtasks("/run", "/tn", TaskName).code == 0; }
        catch { return false; }
    }

    public static bool Enable(string exe)
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>{Program.AppName} — 태스크바에 GPU/CPU 상태 표시</Description></RegistrationInfo>
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId><Delay>PT10S</Delay></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec><Command>"{SecurityElement.Escape(exe)}"</Command><WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exe))}</WorkingDirectory></Exec>
              </Actions>
            </Task>
            """;
        string tmp = Path.Combine(Path.GetTempPath(), $"TaskbarHwMon-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
            return Schtasks("/create", "/tn", TaskName, "/xml", tmp, "/f").code == 0;
        }
        catch { return false; }
        finally { try { File.Delete(tmp); } catch { } }
    }

    public static void Disable()
    {
        try { Schtasks("/delete", "/tn", TaskName, "/f"); } catch { }
    }
}
