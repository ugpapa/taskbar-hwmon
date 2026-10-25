; Taskbar HW Monitor 설치 프로그램 (Inno Setup 6)
; 빌드: build.ps1

#define AppName "Taskbar HW Monitor"
#define AppExe "TaskbarHwMon.exe"
#define AppVersion GetVersionNumbersString("..\publish\" + AppExe)

[Setup]
AppId={{7C3E2A61-4B8D-4F0E-9C2A-5D1B8E6F3A90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=ugpapa
DefaultDirName={autopf}\TaskbarHwMon
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\out
OutputBaseFilename=TaskbarHwMon-Setup-{#AppVersion}
SetupIconFile=..\app\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "autostart"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 작업:"
Name: "desktopicon"; Description: "바탕 화면에 바로 가기 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} 설정"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--register-autostart"; Flags: runhidden waituntilterminated; Tasks: autostart
Filename: "{app}\{#AppExe}"; Description: "{#AppName} 실행"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden waituntilterminated; RunOnceId: "Kill"
Filename: "{app}\{#AppExe}"; Parameters: "--unregister-autostart"; Flags: runhidden waituntilterminated; RunOnceId: "UnregTask"

[Code]
function DesktopRuntimeInstalled(): Boolean;
var
  F: TFindRec;
begin
  // 레지스트리 키는 설치 방식에 따라 없을 수 있어 런타임 폴더를 직접 확인
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), F);
  if Result then FindClose(F);
end;

function InitializeSetup(): Boolean;
var
  Code: Integer;
begin
  Result := True;
  if not DesktopRuntimeInstalled() then
  begin
    if MsgBox('.NET 10 Desktop Runtime이 필요합니다.' #13#10 '다운로드 페이지를 열까요?', mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, Code);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  // 업데이트 설치 시 실행 중인 인스턴스가 파일을 잡고 있으므로 먼저 종료
  if CurStep = ssInstall then
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;
