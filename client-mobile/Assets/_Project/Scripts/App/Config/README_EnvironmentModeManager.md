# Environment Mode Manager 사용 가이드

Unity 에디터에서 dev/release 모드를 쉽게 구분하여 서버 통신 환경을 관리할 수 있는 시스템입니다.

## 📋 개요

- **Dev Mode**: 모든 서버 통신이 localhost 기반으로 동작
- **Release Mode**: 실제 배포 환경의 서버와 통신

## 🚀 설정 방법

### 1. EnvironmentModeManager 생성

1. Unity Editor 메뉴에서 `Tools > Environment Config > Environment Mode Manager > Create Environment Mode Manager` 선택
2. AppPersistent 씬에 `[Environment] Environment Mode Manager` GameObject가 생성됩니다
3. Inspector에서 환경 설정을 조정할 수 있습니다

### 2. 모드별 서버 설정

#### Dev Mode 설정값:
- TCP 서버 포트: 9999 (localhost)
- 싱글 API 서버 포트: 8080 (localhost)
- 인증 서버 포트: 9000 (localhost)
- 웹 서버 포트: 3000 (localhost)

#### Release Mode 설정값:
- 현재 구현된 배포 환경 서버 설정 그대로 사용
- 기본 URL: https://blokus-online.mooo.com

## 🔧 사용 방법

### Inspector에서 직접 설정
1. `[Environment] Environment Mode Manager` GameObject 선택
2. Inspector에서 `Is Development Mode` 체크박스로 모드 변경
3. 각 서버 포트는 Inspector에서 직접 수정 가능

### 에디터 메뉴를 통한 설정
- `Tools > Environment Config > Environment Mode Manager > Set Development Mode`: Dev 모드로 설정
- `Tools > Environment Config > Environment Mode Manager > Set Release Mode`: Release 모드로 설정
- `Tools > Environment Config > Environment Mode Manager > Toggle Mode`: 모드 토글

### Context Menu를 통한 설정 (GameObject 우클릭)
GameObject를 선택한 상태에서 우클릭하면 다음 메뉴들을 사용할 수 있습니다:
- `Toggle Development Mode`: 모드 토글
- `Set Development Mode`: Dev 모드로 설정
- `Set Release Mode`: Release 모드로 설정
- `Test Current Configuration`: 현재 설정 테스트

## 🔍 현재 설정 확인

### 1. 에디터 메뉴
`Tools > Environment Config > Show Current Config`를 선택하면 현재 모든 서버 설정을 확인할 수 있습니다.

### 2. Console 로그
EnvironmentModeManager는 시작할 때 현재 모드와 모든 서버 설정을 Console에 출력합니다.

### 3. NetworkSetup 통합
NetworkSetup에서 `현재 환경 설정 확인` Context Menu를 사용하여 TCP 서버 설정을 확인할 수 있습니다.

## 📁 파일 구조

```
Assets/_Project/Scripts/App/Config/
├── EnvironmentModeManager.cs          # 환경 모드 관리자 (GameObject에 할당)
├── EnvironmentConfig.cs               # 환경 설정 통합 (기존 파일 업데이트)
└── README_EnvironmentModeManager.md   # 이 파일

Assets/Editor/
└── EnvironmentConfigMenu.cs           # Unity Editor 메뉴 (업데이트됨)

Assets/_Project/Scripts/Features/Multi/Net/
└── NetworkSetup.cs                    # TCP 서버 설정 (업데이트됨)
```

## 🔗 통합된 컴포넌트들

### EnvironmentConfig.cs
기존의 EnvironmentConfig는 이제 EnvironmentModeManager와 연동하여 동작합니다:
- Unity Editor에서는 EnvironmentModeManager의 설정을 우선 사용
- EnvironmentModeManager가 없으면 기존 로직 사용 (폴백)
- 빌드 환경에서는 기존 .env 파일 기반 동작

### NetworkSetup.cs
NetworkSetup은 이제 EnvironmentConfig를 통해 자동으로 서버 설정을 가져옵니다:
- `Use Environment Config`: true로 설정하면 자동으로 환경에 맞는 서버 설정 사용
- false로 설정하면 수동 설정값 사용

## ⚙️ 고급 설정

### 포트 커스터마이징
Inspector에서 각 서버의 포트를 개별적으로 설정할 수 있습니다:
- Dev Tcp Port (기본: 9999)
- Dev Api Port (기본: 8080)
- Dev Auth Port (기본: 9000)
- Dev Web Port (기본: 3000)

### 배포 환경 URL 변경
Release 환경의 기본 URL은 Inspector에서 변경할 수 있습니다:
- Release Base Url (기본: https://blokus-online.mooo.com)

## 🐛 문제 해결

### EnvironmentModeManager를 찾을 수 없음
- AppPersistent 씬에 EnvironmentModeManager가 설정되었는지 확인
- GameObject가 활성화되어 있는지 확인
- `Tools > Environment Config > Environment Mode Manager > Create Environment Mode Manager` 메뉴 실행

### 서버 연결 실패
- NetworkSetup에서 `현재 환경 설정 확인` Context Menu로 설정 확인
- Dev 모드일 때는 로컬 서버가 실행 중인지 확인
- Release 모드일 때는 인터넷 연결 및 배포 서버 상태 확인

### 설정이 적용되지 않음
- Unity Editor를 재시작해보세요
- Console에서 EnvironmentModeManager 로그를 확인하세요
- Inspector에서 `Enable Debug Logs`가 활성화되어 있는지 확인하세요

## 📝 사용 예시

```csharp
// 현재 환경 모드 확인
var envManager = EnvironmentModeManager.Instance;
if (envManager != null)
{
    Debug.Log($"Current Mode: {envManager.CurrentMode}");
    Debug.Log($"TCP Server: {envManager.GetTcpServerHost()}:{envManager.GetTcpServerPort()}");
    Debug.Log($"API Server: {envManager.GetApiServerUrl()}");
}

// EnvironmentConfig를 통한 접근 (권장)
string apiUrl = EnvironmentConfig.ApiServerUrl;
string tcpHost = EnvironmentConfig.TcpServerHost;
int tcpPort = EnvironmentConfig.TcpServerPort;
```