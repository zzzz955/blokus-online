# Blokus Online - OIDC Authorization Server

OAuth 2.1/OIDC 기반 통합 인증 서버

## 개요

Blokus Online 프로젝트의 중앙화된 OIDC Authorization Server입니다. 
모든 클라이언트(Web, Desktop, Mobile)에 대해 통일된 인증/인가 서비스를 제공합니다.

## 주요 기능

- **OAuth 2.1/OIDC 호환**: 최신 보안 표준 준수
- **PKCE 지원**: Public client 보안 강화
- **Refresh Token Rotation**: 토큰 재사용 감지 및 차단
- **RS256 JWT**: RSA 서명 기반 토큰
- **Multi-Client 지원**: Web, Desktop, Mobile 클라이언트
- **토큰 관리**: 발급, 갱신, 무효화, 검증

## 지원 클라이언트

### Qt Desktop Client
- **Client ID**: `qt-desktop-client`
- **Type**: Public (PKCE 필수)
- **Flow**: Authorization Code + PKCE
- **Redirect**: `http://localhost:8080/auth/callback` (개발) / 프로덕션 환경변수

### Unity Mobile Client  
- **Client ID**: `unity-mobile-client`
- **Type**: Public (PKCE 필수)
- **Flow**: Authorization Code + PKCE
- **Redirect**: `blokus://auth/callback` (커스텀 URL 스키마 딥링크)

### Next.js Web Client
- **Client ID**: `nextjs-web-client`
- **Type**: Confidential (Client Secret)
- **Flow**: Authorization Code (BFF Pattern)
- **Redirect**: `http://localhost:3000/api/auth/callback/blokus-oidc` (개발) / `https://blokus-online.mooo.com/api/auth/callback/blokus-oidc` (프로덕션)

## API 엔드포인트

### OIDC Discovery
```
GET /.well-known/openid-configuration
```

### Authorization
```
GET /authorize?response_type=code&client_id=...&redirect_uri=...&scope=openid...
POST /authorize  # 로그인 처리
```

### Token Exchange
```
POST /token
Content-Type: application/x-www-form-urlencoded

# Authorization Code Grant
grant_type=authorization_code&client_id=...&code=...&redirect_uri=...&code_verifier=...

# Refresh Token Grant
grant_type=refresh_token&client_id=...&refresh_token=...
```

### Token Introspection
```
POST /introspect
Content-Type: application/x-www-form-urlencoded

token=...&client_id=...&client_secret=...
```

### Token Revocation
```
POST /revocation
Content-Type: application/x-www-form-urlencoded

token=...&client_id=...&client_secret=...
```

### JWKS (Public Keys)
```
GET /jwks.json
```

### Admin (관리자용)
```
GET /admin/status               # 서버 상태
GET /admin/tokens/stats         # 토큰 통계
POST /admin/tokens/cleanup      # 만료된 토큰 정리
POST /admin/keys/rotate         # 키 회전
GET /admin/users/:id/tokens     # 사용자 토큰 조회
DELETE /admin/users/:id/tokens  # 사용자 토큰 무효화
```

## 토큰 수명 (슬라이딩 윈도우)

- **Access Token**: 10분
- **Refresh Token**: 30일 (슬라이딩 윈도우)
- **Refresh Token 최대 수명**: 90일 (절대 만료)
- **Authorization Code**: 10분

### 슬라이딩 윈도우 정책
- 매 토큰 갱신 시 **+30일** 연장
- 최초 로그인 후 **최대 90일**까지 연장 가능
- 90일 후에는 재로그인 필요

## 보안 특징

### Refresh Token Rotation
- 매 refresh 시 새로운 토큰 발급
- 이전 토큰은 즉시 무효화
- 재사용 감지 시 전체 family 무효화

### PKCE (Proof Key for Code Exchange)
- Public client (Desktop, Mobile) 필수
- S256 방식 사용
- Authorization Code 탈취 방지

### Key Rotation
- RSA 2048-bit 키 사용
- 관리자가 수동으로 키 회전 가능
- 이전 키는 백업 저장

## 설치 및 실행

### 환경 설정
```bash
cp .env.example .env
# .env 파일 편집
```

### 개발 환경
```bash
npm install
npm run dev
```

### 프로덕션 환경
```bash
npm install --production
npm start
```

### Docker
```bash
# 개발
docker build --target dev -t blokus-oidc-dev .
docker run -p 9000:9000 blokus-oidc-dev

# 프로덕션
docker build --target prod -t blokus-oidc-prod .
docker run -p 9000:9000 blokus-oidc-prod
```

## 데이터베이스 스키마

### authorization_codes
```sql
CREATE TABLE authorization_codes (
  code_id SERIAL PRIMARY KEY,
  code VARCHAR(255) UNIQUE NOT NULL,
  client_id VARCHAR(255) NOT NULL,
  user_id INTEGER NOT NULL,
  redirect_uri TEXT NOT NULL,
  scope TEXT NOT NULL,
  code_challenge VARCHAR(255),
  code_challenge_method VARCHAR(10),
  expires_at TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT NOW()
);
```

### refresh_token_families
```sql
CREATE TABLE refresh_token_families (
  family_id SERIAL PRIMARY KEY,
  user_id INTEGER NOT NULL,
  client_id VARCHAR(255) NOT NULL,
  device_fingerprint VARCHAR(255),
  status VARCHAR(20) DEFAULT 'active',
  created_at TIMESTAMP DEFAULT NOW(),
  last_used_at TIMESTAMP DEFAULT NOW()
);
```

### refresh_tokens
```sql
CREATE TABLE refresh_tokens (
  token_id SERIAL PRIMARY KEY,
  family_id INTEGER REFERENCES refresh_token_families(family_id),
  jti VARCHAR(255) UNIQUE NOT NULL,
  prev_jti VARCHAR(255),
  status VARCHAR(20) DEFAULT 'active',
  expires_at TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT NOW(),
  last_used_at TIMESTAMP DEFAULT NOW()
);
```

## 환경 변수

| 변수 | 설명 | 기본값 |
|------|------|---------|
| `NODE_ENV` | 실행 환경 | development |
| `PORT` | 서버 포트 | 9000 |
| `OIDC_ISSUER` | OIDC Issuer URL | http://localhost:9000 |
| `ACCESS_TOKEN_LIFETIME` | Access Token 수명 | 10m |
| `REFRESH_TOKEN_LIFETIME` | Refresh Token 수명 (슬라이딩) | 30d |
| `REFRESH_TOKEN_MAX_LIFETIME` | Refresh Token 최대 수명 | 90d |
| `DB_HOST` | 데이터베이스 호스트 | localhost |
| `DB_NAME` | 데이터베이스 이름 | blokus_online |
| `ADMIN_TOKEN` | 관리자 토큰 | - |

## 로깅

- **Winston** 기반 구조화된 로깅
- 파일 로깅: `logs/combined.log`, `logs/error.log`
- 로그 로테이션: 5MB, 5개 파일 유지
- 개발 환경: 콘솔 출력 추가

## 보안 고려사항

1. **프로덕션 배포 시 필수 변경**:
   - `ADMIN_TOKEN` 
   - `WEB_CLIENT_SECRET`
   - 데이터베이스 접속 정보

2. **HTTPS 필수**: 프로덕션에서는 반드시 HTTPS 사용

3. **키 관리**: RSA 키는 자동 생성되며 `keys/` 디렉토리에 저장

4. **CORS 설정**: 허용된 origin만 접근 가능

## 모니터링

### Health Check
```bash
curl http://localhost:9000/health
```

### 상태 조회 (관리자)
```bash
curl -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:9000/admin/status
```

## 개발 가이드

### 새로운 클라이언트 추가
1. `config/oidc.js`의 `clients` 객체에 추가
2. Redirect URI 설정
3. PKCE 요구사항 설정

### 로그 레벨 조정
```bash
export LOG_LEVEL=debug  # debug, info, warn, error
```

### 테스트
```bash
npm test
```

## 문제 해결

### 키 관련 오류
```bash
# 키 재생성
rm -rf keys/
# 서버 재시작하면 새 키 자동 생성
```

### 데이터베이스 연결 실패
- 데이터베이스 서버 상태 확인
- 연결 정보 검증 (`DB_*` 환경변수)
- 스키마 존재 여부 확인

### 토큰 관련 문제
```bash
# 만료된 토큰 정리
curl -X POST -H "Authorization: Bearer $ADMIN_TOKEN" \
  http://localhost:9000/admin/tokens/cleanup
```

## 라이센스

MIT License