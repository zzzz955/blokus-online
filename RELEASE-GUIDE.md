# Blokus Online 릴리즈 가이드

코드 서명이 적용된 서명된 릴리즈를 생성하는 방법을 설명합니다.

## 🔐 1단계: 자체 서명 인증서 생성

처음 한 번만 실행하면 됩니다.

```powershell
# PowerShell을 관리자 권한으로 실행
powershell -Command "Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser"
.\generate-certificate.ps1
```

**인코딩 문제 해결:**
- 모든 스크립트는 UTF-8 인코딩으로 작성되었습니다
- BAT 파일은 `chcp 65001`로 UTF-8 모드를 활성화합니다
- 한글 출력 문제가 있다면 영문 메시지로 표시됩니다

**중요 사항:**
- PowerShell을 **관리자 권한**으로 실행해야 합니다
- 생성된 인증서는 3년간 유효합니다
- 신뢰할 수 있는 루트 인증 기관에 추가하는 것을 권장합니다

## 🔨 2단계: 서명된 빌드 생성

```bat
build-prod-signed.bat
```

이 스크립트는:
- Visual Studio 환경 설정
- CMake를 사용한 Release 빌드
- 자동 코드 서명 적용
- 서명 상태 검증

## 📦 3단계: 서명된 릴리즈 패키징

```bat
package-release-signed.bat
```

버전을 입력하면 (예: 1.2.0):
- `releases/v1.2.0/` 디렉토리 생성
- 서명된 실행 파일과 필요한 라이브러리 패키징
- ZIP 파일 생성 및 체크섬 계산
- 릴리즈 메타데이터 JSON 생성
- `releases/latest/` 최신 버전 참조 업데이트

## 📁 디렉토리 구조

```
releases/
├── v1.0.0/
│   ├── BlokusClient-v1.0.0.zip
│   └── release-info.json
├── v1.1.0/
│   ├── BlokusClient-v1.1.0.zip
│   └── release-info.json
├── latest/                    # 항상 최신 버전을 가리킴
│   ├── BlokusClient-v1.1.0.zip
│   └── release-info.json
└── releases.json              # 전체 릴리즈 목록
```

## 🌐 웹 API 자동 연동

웹 API는 자동으로 `releases/` 디렉토리를 감지합니다:

- **GET** `/api/download/client` - 최신 클라이언트 다운로드
- **POST** `/api/download/client` - 클라이언트 정보 조회
- **GET** `/api/download/version` - 버전 정보 조회

## ✅ 코드 서명 검증

빌드된 실행 파일의 서명 상태 확인:

```powershell
Get-AuthenticodeSignature "build\client\Release\BlokusClient.exe"
```

## 🚀 릴리즈 배포

1. **로컬 테스트**
   ```bat
   # 압축 해제 후 테스트 실행
   cd releases\latest
   # BlokusClient-vX.X.X.zip 압축 해제 후 실행
   ```

2. **Git 커밋 및 태그**
   ```bash
   git add releases/
   git commit -m "Release v1.2.0 with code signing"
   git tag v1.2.0
   git push origin main
   git push origin v1.2.0
   ```

3. **GitHub Releases (선택사항)**
   - GitHub에서 수동으로 릴리즈 생성
   - `releases/vX.X.X/BlokusClient-vX.X.X.zip` 업로드

## 📝 릴리즈 노트 템플릿

```json
{
  "version": "1.2.0",
  "releaseDate": "2024-XX-XXTXX:XX:XX.XXX",
  "platform": "Windows",
  "architecture": "x64",
  "signed": true,
  "fileSize": 15234567,
  "checksum": "sha256:abc123...",
  "changelog": [
    "새로운 기능: XXX 추가",
    "버그 수정: YYY 문제 해결",
    "성능 개선: ZZZ 최적화"
  ]
}
```

## ⚠️ 문제 해결

### 인증서 관련
```powershell
# 인증서 목록 확인
Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*BlokusOnline*" }

# 인증서 삭제 (재생성 필요시)
Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*BlokusOnline*" } | Remove-Item
```

### 빌드 관련
- Visual Studio 2022 Community 설치 확인
- CMake 3.20+ 설치 확인
- vcpkg 의존성 정상 설치 확인

### 서명 실패시
- PowerShell 실행 정책 확인: `Get-ExecutionPolicy`
- 필요시 정책 변경: `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser`
- Windows 개발자 모드 활성화 확인

## 🔒 보안 고려사항

- 자체 서명 인증서는 완전한 신뢰를 제공하지 않습니다
- 사용자는 여전히 "알 수 없는 게시자" 경고를 볼 수 있습니다
- 사용자는 "자세히" → "실행"을 클릭해야 할 수 있습니다
- 상용 코드 서명 인증서 구입을 장기적으로 고려하세요 ($200-400/년)

## 📊 릴리즈 통계

웹 서버가 자동으로 다운로드 통계를 수집합니다:
- 다운로드 횟수
- 사용자 지역 정보
- 다운로드 시간대
- 사용자 에이전트 정보