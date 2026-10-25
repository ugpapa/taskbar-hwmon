# 앱 게시 + 설치 프로그램 빌드 → out\TaskbarHwMon-Setup-<버전>.exe
$ErrorActionPreference = 'Stop'
$env:PATH = "C:\Program Files\dotnet;$env:PATH"
$root = $PSScriptRoot
$publish = Join-Path $root 'publish'

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root 'app\TaskbarHwMon.csproj') -c Release -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish 실패' }

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6(ISCC.exe)이 없습니다: winget install JRSoftware.InnoSetup' }
& $iscc /Q (Join-Path $root 'installer\TaskbarHwMon.iss')
if ($LASTEXITCODE) { throw 'ISCC 실패' }
Get-ChildItem (Join-Path $root 'out') -Filter *.exe | Sort-Object LastWriteTime | Select-Object -Last 1 FullName, Length
