# Unity Mobile Client - 릴리즈 배포 보안 가이드

## 📋 개요

Blokus Unity 모바일 클라이언트의 프로덕션 배포를 위한 보안 강화 작업 가이드입니다.  
**온라인 전용 게임 특성**을 고려한 단계별 구현 계획을 제시합니다.

---

## 🚀 즉시 적용 필수 (릴리즈 블로커, 1-2시간)

### ✅ 1. 환경 분리 설정 (완료)
- Inspector 상에서 dev/staging/prod 엔드포인트 설정 완료
- HttpApiClient에서 환경별 URL 자동 선택

### ✅ 2. 로그/디버그 정리 (진행중)

**GameLogger.cs 적용**:
```bash
# Visual Studio/Rider에서 전체 교체
Find: Debug\.Log\(
Replace: GameLogger.Log(

Find: Debug\.LogError\(  
Replace: GameLogger.LogError(

Find: Debug\.LogWarning\(
Replace: GameLogger.LogWarning(
```

**중요**: 민감 정보 로그 확인 및 제거
- 사용자 토큰, 비밀번호, 개인정보
- API 요청/응답 상세 내용
- 디바이스 고유 식별자

### ✅ 3. Unity 코드 난독화 설정

**Player Settings 경로**: File → Build Settings → Player Settings

```yaml
Other Settings:
  ✅ Scripting Backend: IL2CPP
  ✅ Api Compatibility Level: .NET Standard 2.1  
  ✅ Managed Stripping Level: High
  ❌ Use Incremental GC: 해제

Android Settings:
  ✅ Minify: Release
  ✅ Use R8: 활성화
  ✅ Proguard: Custom + Built-in

iOS Settings:
  ✅ Stripping Level: High
  ✅ Script Call Optimization: Fast (Release Only)
```

---

## 📅 단기 적용 권장 (1-2일)

### 🔄 4. Refresh Token 시스템 구현

#### Phase 1: 기본 구조 (1일)

**4.1. TokenManager.cs 생성**
```csharp
// Assets/_Project/Scripts/App/Services/TokenManager.cs
namespace App.Services
{
    public class TokenManager : MonoBehaviour 
    {
        // Unity Keychain/Keystore 래퍼
        public void StoreRefreshToken(string token)
        public string GetRefreshToken()
        public void ClearRefreshToken()
        
        // 토큰 검증 및 갱신
        public async Task<TokenRefreshResult> RefreshAccessToken()
        public bool IsRefreshTokenValid()
    }
}
```

**4.2. HttpApiClient 확장**
```csharp
// 401 응답시 자동 재시도 로직 추가
private async Task<T> HandleAuthFailure<T>(Func<Task<T>> originalRequest)
{
    var refreshResult = await TokenManager.Instance.RefreshAccessToken();
    if (refreshResult.Success) {
        return await originalRequest(); // 재시도
    }
    
    // Refresh 실패 → 강제 로그아웃
    OnAuthResponse?.Invoke(false, "세션 만료", null);
    return default(T);
}
```

#### Phase 2: 서버 통합 (2일)

**4.3. 서버 API 엔드포인트 추가**
```bash
POST /auth/refresh
{
  "refresh_token": "current_refresh_token",
  "device_fingerprint": "optional_device_id"
}

Response:
{
  "access_token": "new_jwt_token",
  "refresh_token": "new_refresh_token", 
  "expires_in": 900
}
```

**4.4. 서버 DB 스키마**
```sql
-- refresh_tokens 테이블 추가
CREATE TABLE refresh_tokens (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    token_hash VARCHAR(255) UNIQUE,
    family_id UUID,
    issued_at TIMESTAMP DEFAULT NOW(),
    last_used TIMESTAMP DEFAULT NOW(),
    expires_at TIMESTAMP,
    is_revoked BOOLEAN DEFAULT FALSE,
    device_fingerprint VARCHAR(255)
);

-- 인덱스 추가
CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_family_id ON refresh_tokens(family_id);
CREATE INDEX idx_refresh_tokens_hash ON refresh_tokens(token_hash);
```

### 🔐 5. 시크릿 관리 정리

**5.1. 하드코딩 제거 대상**
```bash
# 검색 패턴
grep -r "api.*key\|secret\|password" Assets/_Project/Scripts/
grep -r "localhost\|127\.0\.0\.1" Assets/_Project/Scripts/
grep -r "jwt.*secret" Assets/_Project/Scripts/
```

**5.2. 환경 설정 분리**
```csharp
// ServerConfig.cs 생성
[CreateAssetMenu(fileName = "ServerConfig", menuName = "Game/Server Config")]
public class ServerConfig : ScriptableObject 
{
    [Header("Environment URLs")]
    public string productionUrl = "https://api.blokus.com";
    public string stagingUrl = "https://staging-api.blokus.com";
    public string developmentUrl = "http://localhost:8080";
    
    [Header("Public Keys Only")]
    public string publicEncryptionKey; // 서버에서 전달받는 공개키
}
```

---

## 🔮 중장기 고급 보안 (추후 적용)

### 6. 토큰 회전 정책 상세

#### 서버 검증 플로우 설계
```yaml
Step 1: 클라이언트 refresh 전송
  - 현재 refresh_token + device_fingerprint

Step 2: 서버 기본 검증
  - 토큰 해시 일치 확인
  - 만료 시간 검증
  - 취소 상태 확인

Step 3: 패밀리 검증 (토큰 회전 추적)
  - 최신 토큰: 성공 → 새 토큰 발급
  - 이전 토큰: DB 조회 → 슬라이딩 범위 확인
  - 의심 토큰: 패밀리 전체 무효화

Step 4: 보안 정책 적용
  - 성공: 새 access + refresh 반환
  - 실패: 강제 로그아웃 + 패밀리 삭제
```

#### 고급 보안 옵션
```yaml
토큰 패밀리 관리:
  - 회전시마다 새 family_id 생성
  - 구 토큰 즉시 무효화
  - 의심 활동시 패밀리 전체 삭제

세션 관리:
  - 동시 로그인 제한 (3-5대)
  - 디바이스 지문 추적
  - 비정상 패턴 감지

강제 로그아웃:
  - 보안 사고시 전체 사용자 로그아웃
  - 특정 사용자 원격 로그아웃
  - 디바이스별 세션 관리
```

### 7. 추가 보안 강화

**7.1. 인증서 핀닝 (선택)**
```csharp
// UnityWebRequest 인증서 검증
public class CertificatePinning : CertificateHandler
{
    private static readonly string[] PinnedCertificates = {
        "SHA256:ABCD1234...", // prod 인증서
        "SHA256:EFGH5678..."  // staging 인증서
    };
}
```

**7.2. 크래시 수집 (선택)**
```csharp
// Firebase Crashlytics 또는 Unity Cloud Diagnostics
#if !DEVELOPMENT
    Crashlytics.SetCustomKey("user_id", currentUserId);
    Crashlytics.Log("Game state at crash");
#endif
```

**7.3. 레이트 리밋 (서버 구현)**
```yaml
서버 정책:
  - 로그인: 5회/분
  - API 호출: 100회/분
  - 스테이지 완료: 10회/분

클라이언트 대응:
  - 429 응답시 지수 백오프
  - 로컬 요청 제한 (선택)
```

---

## 🎯 구현 우선순위 및 타임라인

### 즉시 적용 (릴리즈 준비, 2시간)
1. **로그 정리**: GameLogger 전체 적용
2. **난독화 설정**: Unity Player Settings 변경
3. **빌드 테스트**: Release 빌드 검증

### 단기 적용 (보안 강화, 3일)  
4. **TokenManager**: Unity Keychain/Keystore 연동
5. **HttpApiClient 확장**: 401 자동 재시도
6. **서버 API**: `/auth/refresh` 엔드포인트
7. **DB 스키마**: refresh_tokens 테이블

### 중장기 적용 (고급 보안, 1-2주)
8. **토큰 회전**: 패밀리 기반 관리
9. **보안 정책**: 강제 로그아웃, 세션 제한
10. **모니터링**: 크래시 수집, 비정상 패턴 감지

---

## 📝 체크리스트

### 릴리즈 전 필수 확인사항
- [ ] Development Build 해제 확인
- [ ] 모든 Debug.Log → GameLogger 교체
- [ ] 민감 정보 로그 제거 확인
- [ ] Unity Player Settings 난독화 설정
- [ ] 프로덕션 API 엔드포인트 설정
- [ ] Release 빌드 테스트 완료

### 단기 보안 강화 확인사항  
- [ ] TokenManager 구현 완료
- [ ] Keychain/Keystore 연동 테스트
- [ ] 401 자동 재시도 로직 검증
- [ ] 서버 refresh API 구현
- [ ] DB 스키마 마이그레이션

### 장기 보안 고도화 확인사항
- [ ] 토큰 패밀리 회전 시스템
- [ ] 강제 로그아웃 API
- [ ] 크래시 수집 시스템
- [ ] 보안 모니터링 대시보드

---

## 🔧 참고 리소스

### Unity 보안 가이드
- [Unity Best Practices for Mobile](https://docs.unity3d.com/Manual/MobileBestPractices.html)
- [IL2CPP Code Stripping](https://docs.unity3d.com/Manual/IL2CPP-BytecodeStripping.html)
- [Android Build Settings](https://docs.unity3d.com/Manual/android-BuildProcess.html)

### 토큰 보안 참고
- [OWASP JWT Security](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
- [Refresh Token Rotation](https://auth0.com/blog/refresh-tokens-what-are-they-and-when-to-use-them/)
- [Mobile App Security](https://owasp.org/www-project-mobile-top-10/)

### Unity 네이티브 플러그인
- [Unity iOS Keychain](https://github.com/yasirkula/UnityIOSKeychain)
- [Unity Android Keystore](https://github.com/yasirkula/UnityAndroidKeystore)
- [Unity Secure PlayerPrefs](https://assetstore.unity.com/packages/tools/utilities/secure-playerprefs-12607)

---

**⚠️ 중요**: 이 가이드는 Blokus 온라인 전용 게임의 특성(항상 서버 연결 필요)을 고려하여 설계되었습니다.  
**🎯 목표**: 최소 보안 요구사항 충족 → 점진적 보안 강화