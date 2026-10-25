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
; 설치할 때마다 언어를 묻는다 (기본 선택은 Windows 표시 언어). 고른 언어는 앱 설정의 기본 언어가 된다
ShowLanguageDialog=yes
UsePreviousLanguage=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
korean.AppLang=ko
english.AppLang=en
japanese.AppLang=ja
chinesesimplified.AppLang=zh-Hans

korean.Extra=추가 작업:
english.Extra=Additional tasks:
japanese.Extra=追加タスク:
chinesesimplified.Extra=附加任务:

korean.AutoStart=Windows 시작 시 자동 실행
english.AutoStart=Start with Windows
japanese.AutoStart=Windows 起動時に自動実行
chinesesimplified.AutoStart=开机时自动运行

korean.ReplaceWidgets=Windows 11 위젯 대신 사용 (기본 위젯·날씨 버튼 제거, 탐색기 재시작)
english.ReplaceWidgets=Use instead of Windows 11 Widgets (remove the built-in Widgets/weather button, restarts Explorer)
japanese.ReplaceWidgets=Windows 11 ウィジェットの代わりに使用 (標準ウィジェット・天気ボタンを削除、エクスプローラーを再起動)
chinesesimplified.ReplaceWidgets=替代 Windows 11 小组件 (移除内置小组件/天气按钮，并重启资源管理器)

korean.DesktopIcon=바탕 화면에 바로 가기 만들기
english.DesktopIcon=Create a desktop shortcut
japanese.DesktopIcon=デスクトップにショートカットを作成
chinesesimplified.DesktopIcon=创建桌面快捷方式

korean.SettingsShortcut=설정
english.SettingsShortcut=Settings
japanese.SettingsShortcut=設定
chinesesimplified.SettingsShortcut=设置

korean.RemovingWidgets=Windows 위젯 제거 중...
english.RemovingWidgets=Removing Windows Widgets...
japanese.RemovingWidgets=Windows ウィジェットを削除しています...
chinesesimplified.RemovingWidgets=正在移除 Windows 小组件...

korean.LaunchApp=%1 실행
english.LaunchApp=Launch %1
japanese.LaunchApp=%1 を起動
chinesesimplified.LaunchApp=运行 %1

korean.NeedRuntime=.NET 10 Desktop Runtime이 필요합니다.%n다운로드 페이지를 열까요?
english.NeedRuntime=.NET 10 Desktop Runtime is required.%nOpen the download page?
japanese.NeedRuntime=.NET 10 Desktop Runtime が必要です。%nダウンロードページを開きますか？
chinesesimplified.NeedRuntime=需要 .NET 10 Desktop Runtime。%n是否打开下载页面？

[Tasks]
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:Extra}"
Name: "replacewidgets"; Description: "{cm:ReplaceWidgets}"; GroupDescription: "{cm:Extra}"; Flags: unchecked
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Extra}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} {cm:SettingsShortcut}"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[InstallDelete]
; 언어를 바꿔 다시 설치하면 이전 언어의 설정 바로 가기가 남지 않게
Type: files; Name: "{group}\{#AppName} *.lnk"

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--register-autostart"; Flags: runhidden waituntilterminated; Tasks: autostart
Filename: "{app}\{#AppExe}"; Parameters: "--replace-widgets"; StatusMsg: "{cm:RemovingWidgets}"; Flags: runhidden waituntilterminated; Tasks: replacewidgets
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden waituntilterminated; RunOnceId: "Kill"
Filename: "{app}\{#AppExe}"; Parameters: "--unregister-autostart"; Flags: runhidden waituntilterminated; RunOnceId: "UnregTask"
; 이 앱이 위젯을 제거했던 경우에만 되돌림
Filename: "{app}\{#AppExe}"; Parameters: "--restore-widgets"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreWidgets"

[UninstallDelete]
Type: files; Name: "{app}\language.txt"

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
    if MsgBox(CustomMessage('NeedRuntime'), mbConfirmation, MB_YESNO) = IDYES then
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
  // 설치 언어를 앱의 기본 언어로 (설정 창에서 바꾸면 그쪽이 우선)
  if CurStep = ssPostInstall then
    SaveStringToFile(ExpandConstant('{app}\language.txt'), CustomMessage('AppLang'), False);
end;
