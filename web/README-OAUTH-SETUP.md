# Google OAuth 설정 가이드

## 1. Google Cloud Console 설정

1. [Google Cloud Console](https://console.cloud.google.com/) 접속
2. 새 프로젝트 생성 또는 기존 프로젝트 선택
3. "API 및 서비스" → "OAuth consent screen" 설정
   - User Type: External
   - App name: "Blokus Online"
   - User support email: 본인 이메일
   - Developer contact information: 본인 이메일
4. "API 및 서비스" → "Credentials" 이동
5. "+ CREATE CREDENTIALS" → "OAuth 2.0 Client IDs"
6. Application type: "Web application"
7. Name: "Blokus Online Web"
8. Authorized redirect URIs 추가:
   ```
   http://localhost:3000/api/auth/callback/google
   ```
9. CREATE 클릭
10. 생성된 Client ID와 Client Secret 복사

## 2. .env 파일 설정

현재 .env 파일의 다음 항목을 실제 값으로 변경:

```bash
# Google OAuth (실제 값으로 변경 필요)
GOOGLE_CLIENT_ID="실제-google-client-id"
GOOGLE_CLIENT_SECRET="실제-google-client-secret"
```

## 3. 테스트

1. `npm run dev` 실행
2. `http://localhost:3000/auth/signin` 접속
3. "Google로 시작하기" 버튼 클릭
4. Google 로그인 진행
5. ID/PW 설정 페이지로 이동 확인

## 임시 테스트 (Google OAuth 없이)

Google OAuth 설정 전에 페이지 렌더링을 확인하려면:

```bash
# .env 파일에 임시 값 설정
GOOGLE_CLIENT_ID="test-client-id"
GOOGLE_CLIENT_SECRET="test-client-secret"
```

이렇게 하면 페이지는 렌더링되지만 실제 Google 로그인은 작동하지 않습니다.