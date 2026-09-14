# MyOnOff

MyOnOff는 같은 로컬 네트워크에 있는 Windows 호스트 PC를 Windows 또는 Android 기기에서 제어하는 개인용 전원 관리 도구입니다.

- Wake-on-LAN으로 절전 또는 완전 종료된 호스트를 켭니다.
- 인증된 로컬 API로 절전과 정상 종료를 요청합니다.
- Host Agent와 SMB 저장소가 모두 준비된 경우에만 `ONLINE`으로 표시합니다.
- 인터넷이나 클라우드 서비스 없이 가정 내 LAN에서 동작합니다.

[`Plan.md`](Plan.md)는 기존 MVP 아키텍처와 범위의 기준 문서입니다. 현재 검증 기준과 최종 마감 요구사항은 [`MyOnOff_Final_Handoff_Additional_Requirements.md`](mds/MyOnOff_Final_Handoff_Additional_Requirements.md)를 따릅니다. 이전 영문 README는 [`README_2026-09-13_EN.md`](README_2026-09-13_EN.md)에 보존했습니다.

## 주요 기능

### 전원 제어

- **ON**: 설정된 MAC 주소로 WOL 매직 패킷을 전송합니다.
- **Sleep**: Host Agent에 인증된 절전 요청을 보냅니다.
- **Shutdown**: 확인 후 Host Agent에 인증된 정상 종료 요청을 보냅니다.
- 사용자 동작은 백그라운드 상태 확인 때문에 유실되지 않으며, 처리 중 중복 동작은 차단됩니다.

### 상태 판정

- **OFFLINE**: LAN은 사용 가능하지만 호스트, Agent, SMB에서 유효한 응답이 없습니다.
- **BOOTING**: 일부 네트워크 신호는 확인되지만 Agent와 SMB가 모두 준비되지 않았습니다.
- **ONLINE**: 호환되는 Agent 응답과 SMB 준비 상태가 모두 확인되었습니다.
- **UNKNOWN**: LAN 사용 불가, 잘못된 응답, 예상 호스트 불일치 등으로 상태를 확정할 수 없습니다.
- **GOING TO SLEEP / SHUTTING DOWN**: 전원 요청 후 전환을 확인하는 상태입니다.

### Easy Mode

Windows와 Android 모두 기존 상태 확인과 WOL 로직을 그대로 사용하는 Easy Mode를 제공합니다.

- `OFFLINE`: 큰 ON 버튼 표시
- `BOOTING`: 켜지는 중이라는 진행 표시
- `ONLINE`: 호스트 준비 완료 표시
- `UNKNOWN`: ON 버튼 없이 중립적인 상태 확인 화면 표시

Easy Mode에서는 IP, 포트, Agent, SMB, 지연시간, Sleep, Shutdown 같은 관리 정보를 숨깁니다. Settings에서 **Start app in Easy Mode**를 선택하면 다음 실행부터 Easy Mode로 시작합니다.

## 구성 요소

```text
src/MyOnOff.Protocol/           공용 API 계약, WOL 패킷, CIDR 및 상태 판정
src/MyOnOff.HostAgent/          호스트 PC에서 실행되는 ASP.NET Core Agent
src/MyOnOff.DesktopController/  다른 Windows PC에서 사용하는 WPF 컨트롤러
tests/MyOnOff.Protocol.Tests/   공용 로직과 UI 상태 계약 테스트
android/app/                    Kotlin/Jetpack Compose Android 컨트롤러
scripts/                        빌드, 배포, 방화벽 및 자동 시작 도구
mds/assets/myonoff-icon-source.png  승인된 앱 아이콘 원본
docs/API.md                     Host Agent API 규격
docs/PACKAGING.md               Windows/Android 패키징과 로컬 릴리스 절차
MANUAL_VALIDATION.md            실기기 및 LAN 수동 검증 절차
IMPLEMENTATION_STATUS.md        자동 검증 결과와 남은 확인 항목
mds/                            후속 검증, UX 및 패키징 요구사항
```

## 개발 요구사항

### Windows

- Windows 10 또는 11
- 빌드용 .NET SDK 8.0.x
- framework-dependent Desktop Controller 실행 시 .NET 8 Desktop Runtime x64
- self-contained 배포본은 대상 PC에 별도 .NET 설치가 필요하지 않습니다.

### Android

- Android Studio 및 Android SDK 37
- Android Gradle Plugin 9.2.1
- Gradle Wrapper 9.4.1
- 호환 JDK—현재 프로젝트는 Android Studio JBR 25로 검증했습니다.

Android 17/API 37에서는 로컬 TCP와 UDP 브로드캐스트를 위해 `ACCESS_LOCAL_NETWORK` 권한이 필요합니다. 앱은 이 권한을 선언하고 실행 중 요청합니다.

## Windows 빌드와 테스트

저장소 루트에서 실행합니다.

```powershell
dotnet restore MyOnOff.sln
dotnet build MyOnOff.sln -c Release --no-restore
dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release --no-build
```

추가 정적·의미 검사는 다음과 같습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\static-check.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\semantic-check.ps1
```

## Windows 배포본 만들기

다음 명령은 세 가지 Windows 배포본을 `artifacts/` 아래에 만듭니다.

```powershell
.\scripts\publish-windows.ps1
```

```text
artifacts/
  host-agent-selfcontained/
  desktop-controller/
  desktop-controller-selfcontained/
```

일반 사용자는 선택한 Desktop Controller 디렉터리 전체를 원하는 위치에 복사한 뒤 `MyOnOff.DesktopController.exe`를 실행하면 됩니다. `dotnet run`은 개발할 때만 필요합니다.

대상별 publish, 바탕화면 바로가기, Host Agent 안전 업데이트 방법은 [`docs/PACKAGING.md`](docs/PACKAGING.md)를 참고하십시오. `artifacts/`는 Git에서 제외됩니다.

Desktop Controller 실행 파일, 창, 작업 표시줄에는 승인된 MyOnOff 아이콘이 사용됩니다. 아이콘을 다시 파생해야 할 때는 원본 디자인을 수정하지 않고 다음 명령을 실행합니다.

```powershell
.\scripts\generate-icons.ps1
```

## Host Agent 설정

저장소에는 인증 토큰이 포함되지 않습니다. 호스트 PC에서는 다음 중 하나로 토큰을 설정합니다.

1. 권장: 관리자 PowerShell에서 시스템 환경 변수 `MYONOFF_AGENT_TOKEN`을 설정하고 Agent 작업을 다시 시작합니다.
2. `appsettings.Local.example.json`을 `appsettings.Local.json`으로 복사하고 토큰을 입력한 뒤 파일 권한을 Administrators와 SYSTEM으로 제한합니다.

기본 설정은 다음 환경을 기준으로 합니다.

```text
Host IP:       192.168.219.104
Agent port:    5055
Allowed LAN:   192.168.219.0/24
SMB port:      445
SMB share:     domination
```

Host Agent self-contained 배포본을 대화형으로 확인하려면 다음처럼 실행합니다.

```powershell
$env:MYONOFF_AGENT_TOKEN = '<컨트롤러와 동일한 긴 임의 토큰>'
.\artifacts\host-agent-selfcontained\MyOnOff.HostAgent.exe
```

다른 터미널에서 상태를 확인합니다.

```powershell
.\scripts\check-agent-status.ps1
```

API 규격은 [`docs/API.md`](docs/API.md)에 있습니다. Agent 로그는 기본적으로 `%ProgramData%\MyOnOff\host-agent.log`에 기록됩니다.

## 방화벽과 자동 시작

관리자 PowerShell에서 먼저 변경 내용을 미리 확인한 뒤 적용합니다.

```powershell
.\scripts\configure-host-firewall.ps1 -WhatIf
.\scripts\configure-host-firewall.ps1

.\scripts\install-host-startup.ps1 `
  -PublishedAgentPath 'C:\myonoff\host-agent-selfcontained\MyOnOff.HostAgent.exe' `
  -WhatIf

.\scripts\install-host-startup.ps1 `
  -PublishedAgentPath 'C:\myonoff\host-agent-selfcontained\MyOnOff.HostAgent.exe'
```

등록된 작업은 SYSTEM 계정으로 Windows 시작 시 실행됩니다. 제거할 때는 `scripts\remove-host-startup.ps1`을 사용합니다.

## 컨트롤러 설정

Windows 또는 Android 앱의 **Settings**에서 다음 값을 입력합니다.

- Host IP
- 유선 Ethernet MAC 주소
- 브로드캐스트 IP
- WOL·Agent·SMB 포트
- SMB 공유 이름
- Host Agent와 동일한 인증 토큰
- 예상 호스트 이름
- 필요하면 Easy Mode 시작 옵션

Windows 설정은 `%LocalAppData%\MyOnOff\controller-settings.json`에 저장되고 Android 설정은 앱 전용 Preferences에 저장됩니다. 인증 토큰은 로그에 기록되지 않습니다.

## Android 빌드

저장소 루트에서 원클릭 빌드 스크립트를 실행하면 Gradle Wrapper로 APK를 만들고 `artifacts/android/`에 복사합니다.

```powershell
.\scripts\build-android.ps1 -Configuration Debug
.\scripts\build-android.ps1 -Configuration Release
```

출력 파일:

```text
artifacts/android/MyOnOff-<version>-debug.apk
artifacts/android/MyOnOff-<version>-release.apk
```

릴리스 APK에는 영구 보관할 사용자 소유 키 저장소가 필요합니다. `android/signing.properties.example`을 `android/signing.properties`로 복사하고 실제 키 저장소와 비밀번호를 로컬에서만 설정합니다. 키 저장소를 잃으면 같은 앱의 후속 버전을 기존 설치 위에 업데이트하지 못할 수 있습니다.

```powershell
cd android
.\gradlew.bat verifyReleaseSigningConfiguration
.\gradlew.bat testDebugUnitTest
```

키 저장소, 비밀번호, 개인 키와 실제 `signing.properties`는 Git에서 제외됩니다. 최초 키 생성, 백업, debug에서 release 설치로 전환하는 방법은 [`docs/PACKAGING.md`](docs/PACKAGING.md)에 있습니다.

Android 런처 아이콘은 Windows와 같은 승인 원본에서 생성된 표준 밀도별 bitmap 자산을 사용합니다. foreground/background 분리가 원본 재설계를 요구하므로 adaptive icon은 의도적으로 추가하지 않았습니다.

## 보안 원칙

- Host Agent는 로컬 사설망 주소만 허용합니다.
- Windows 방화벽 규칙도 로컬 서브넷으로 제한합니다.
- Sleep과 Shutdown은 Bearer 토큰 인증이 필요합니다.
- 토큰은 소스, URL, 로그, 배포 바이너리에 포함하지 않습니다.
- 클라우드 로그인, 외부 인증 서버, 포트 자동 개방은 사용하지 않습니다.

## 검증 현황

기존 MVP의 Windows·Android·Host Agent 실기기 검증 결과는 [`mds/Validation_Status.md`](mds/Validation_Status.md)에 있습니다.

Easy Mode와 패키징 변경 후 자동 검증 결과 및 남은 항목은 [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md), 실제 LAN과 UI 재검증 순서는 [`MANUAL_VALIDATION.md`](MANUAL_VALIDATION.md)를 참고하십시오.
