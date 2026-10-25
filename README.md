# Taskbar HW Monitor

Windows 11 태스크바 왼쪽 빈 공간에 GPU·CPU 상태를 실시간으로 보여 주는 작은 앱입니다.

![태스크바 표시](docs/taskbar.png)

| 줄 | 사용률 | 온도 | 전력 | 클럭 | 전압 | 메모리 클럭 | 팬 |
|---|---|---|---|---|---|---|---|
| **GPU** | GPU Core 부하 | GPU Core | 보드 전력 | 코어 클럭 | 코어 전압 | VRAM 클럭 | GPU 팬 RPM |
| **CPU** | 전체 부하 | 패키지 | 패키지 전력 | 코어 평균 클럭 | 코어 전압 | DRAM 클럭 | CPU 쿨러 RPM |

- 온도가 80°C 이상이면 주황, 90°C 이상이면 빨강으로 표시됩니다.
- 태스크바의 자식 창으로 붙기 때문에 전체 화면 게임에서는 태스크바와 함께 자동으로 숨겨집니다.
- 배경은 실제 태스크바 색을 읽어 투명 처리하고 ClearType으로 그려서 글자가 선명합니다.
- 탐색기가 재시작되어도 2초 안에 태스크바에 다시 붙습니다.

## 설치

[Releases](../../releases)에서 `TaskbarHwMon-Setup-x.y.z.exe`를 받아 실행합니다.

- 필요: Windows 10/11 x64, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- 설치 시 **Windows 시작 시 자동 실행**을 선택하면 작업 스케줄러에 등록되어 로그인할 때 UAC 창 없이 관리자 권한으로 실행됩니다.
- 제거는 Windows **설정 › 앱**에서 할 수 있습니다.

> CPU 온도·전력·전압과 메인보드 팬 RPM을 읽으려면 관리자 권한이 필요합니다 ([LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) + PawnIO 드라이버).
> PawnIO가 설치되어 있지 않으면 일부 CPU 값이 `-`로 나올 수 있습니다.

## 사용법

- **설정 열기:** 트레이 아이콘을 클릭하거나, 시작 메뉴에서 앱을 한 번 더 실행하거나, 태스크바 숫자 위를 우클릭합니다.
- **종료:** 트레이 아이콘을 우클릭한 뒤 **종료**를 누릅니다.

![설정 창](docs/settings.png)

설정 창에서 바꾼 값은 태스크바에 바로 미리 보이며, **확인**을 눌러야 저장됩니다.

| 설정 | 설명 |
|---|---|
| 표시 항목 | GPU/CPU 항목을 하나씩 켜고 끌 수 있습니다. 한 줄의 항목을 모두 끄면 그 줄은 숨겨집니다 |
| CPU 쿨러 팬 | 메인보드 팬 중 CPU 쿨러에 해당하는 것을 고릅니다 (현재 RPM이 함께 표시됨) |
| 왼쪽 여백 | 위젯 버튼과 겹치면 늘려 주세요 |
| 글자 크기 / 굵게 | |
| 갱신 간격 | 0.5 / 1 / 2 / 5초 |

설정 파일은 `%APPDATA%\TaskbarHwMon\config.json`에 저장됩니다.

### 참고

- DRAM은 실시간 클럭 센서가 없어서 메모리 설정 속도(MT/s)의 절반, 즉 실제 클럭을 표시합니다. DDR4-3200이면 1600MHz로 나옵니다.
- CPU 클럭은 모든 코어 클럭의 평균입니다.

## 빌드

```powershell
winget install Microsoft.DotNet.SDK.10
winget install JRSoftware.InnoSetup
./build.ps1      # publish\ 게시 + out\TaskbarHwMon-Setup-<버전>.exe 생성
```

| 경로 | 내용 |
|---|---|
| `app/` | WinForms 앱 (.NET 10) |
| `installer/TaskbarHwMon.iss` | Inno Setup 스크립트 |
| `tools/make-icon.ps1` | `app/app.ico` 생성 |

## 라이선스

MIT. 센서 읽기에는 [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)(MPL-2.0)를 사용합니다.
