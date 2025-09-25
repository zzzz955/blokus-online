# 블로블로 (Bloblo)

[![GitHub Release](https://img.shields.io/github/v/release/zzzz955/blokus-online?display_name=tag)](https://github.com/zzzz955/blokus-online/releases)
[![License](https://img.shields.io/github/license/zzzz955/blokus-online)](LICENSE)

> 실시간 멀티플레이어 지원 블로커스 룰 기반 퍼즐 게임 - 데스크톱, 모바일, 웹 플랫폼 통합 솔루션

## 1. 소개

블로블로(Bloblo)는 보드게임 블로커스를 디지털화한 실시간 멀티플레이어 게임입니다. C++/Qt 데스크톱 클라이언트, Unity 모바일 앱, Next.js 웹 애플리케이션을 제공하며, 강력한 서버 인프라와 OAuth 2.1/OIDC 인증 시스템을 통해 안전하고 확장 가능한 게임 환경을 제공합니다.

## 2. 모노레포 구조

| 경로 | 역할/설명 |
|------|-----------|
| `client/` | Qt/C++ 데스크톱 클라이언트 (Windows/macOS/Linux) |
| `client-mobile/` | Unity C# 모바일 앱 (Android/iOS) |
| `web/` | Next.js 웹 애플리케이션 (로비/관리자) |
| `server/` | C++ TCP 게임 서버 (멀티플레이어 로직) |
| `single-player-api/` | Node.js REST API (싱글플레이어 모드) |
| `oidc-auth-server/` | OAuth 2.1/OIDC 인증 서버 (Express.js) |
| `common/` | 공통 C++ 라이브러리 (게임 로직/유틸) |
| `proto/` | Protocol Buffers 메시지 정의 |
| `nginx/` | Nginx 리버스 프록시 설정 |
| `database/` | PostgreSQL 스키마 및 마이그레이션 |

## 3. 기술 스택

**프론트엔드:**
- Qt 6.5+ (C++ 데스크톱 UI)
- Unity 2022.3 LTS (C# 모바일)
- Next.js 14 + TypeScript (웹)
- Tailwind CSS + Prisma ORM

**백엔드:**
- C++17 + CMake (게임 서버)
- Node.js + Express.js (API 서버)
- Protocol Buffers (메시지 직렬화)

**데이터베이스 & 인프라:**
- PostgreSQL 16 (주 데이터베이스)
- Redis 7 (세션/캐시)
- Nginx (리버스 프록시)
- Docker + Docker Compose

**인증 & 보안:**
- OAuth 2.1 + OpenID Connect
- JWT (액세스 토큰)
- Argon2 (패스워드 해싱)

## 4. 개발 환경 구성

### Docker Compose 기반 통합 환경

본 프로젝트는 Docker Compose를 중심으로 한 마이크로서비스 아키텍처로 구성되었습니다:

```bash
# 전체 서비스 실행
docker compose up -d

# 개발용 로컬 데이터베이스만 실행
docker compose up postgres redis -d
```

### 워크스페이스별 개발 환경

개발 과정에서 각 서비스별로 독립적인 개발환경을 구축하여 사용했습니다:

- **웹**: Next.js 개발서버 (포트 3000)
- **API**: Node.js + nodemon (포트 8080)
- **OIDC**: Express.js 개발서버 (포트 9000)
- **게임서버**: CMake + vcpkg를 통한 네이티브 빌드

**개발 포트 매핑:**
- 웹사이트: http://localhost:3000
- 게임 서버: localhost:9999 (TCP)
- 싱글플레이어 API: http://localhost:8080
- OIDC 서버: http://localhost:9000

## 5. 개발 환경 요구사항

### 네이티브/데스크톱 (C++)
- CMake ≥ 3.24
- Qt 6.5+ (Core, Widgets, Network, Multimedia)
- vcpkg 패키지 매니저
- MSVC 2022 또는 Clang 15+
- Protocol Buffers
- PostgreSQL 클라이언트 라이브러리

### 웹/TypeScript
- Node.js ≥ 20.0
- npm ≥ 10.0
- PostgreSQL 16

### 서버
- Node.js ≥ 18.0 (OIDC/Single API)
- Redis 7+ (캐싱)
- PostgreSQL 16
- Docker & Docker Compose

### 모바일 (Unity)
- Unity 2022.3 LTS
- Android SDK (API 24+) / Xcode 14+
- .NET Standard 2.1

## 6. 개발 환경 구성 세부사항

### C++ 빌드 시스템 구성

vcpkg 패키지 매니저와 CMake Preset을 활용한 의존성 관리:
- Protocol Buffers, PostgreSQL, Qt6 등 주요 라이브러리 통합
- Windows/Linux 크로스 플랫폼 빌드 지원
- Visual Studio Code/Visual Studio 통합 개발환경

### Node.js 서비스 아키텍처

각 Node.js 서비스는 독립적인 package.json과 환경설정을 가지며:
- TypeScript + ESLint + Prettier 통합 개발환경
- nodemon을 통한 핫 리로드 개발
- Jest 기반 단위/통합 테스트 환경

### 코드 품질 관리
- C++: clang-format (Google 스타일)
- TypeScript/JavaScript: ESLint + Prettier
- C# (Unity): Unity 코딩 컨벤션
- Git Hooks를 통한 커밋 전 자동 포맷팅

## 7. 아키텍처 개요

```
클라이언트 계층:
[Desktop Client] ──TCP:9999──→ [Game Server] ──→ [PostgreSQL]
[Mobile Client]  ──┬─TCP:9999──→ [Game Server] ──→ [PostgreSQL]
                   ├─HTTP──────→ [Auth Server] ──→ [PostgreSQL]
                   └─HTTP──────→ [Single API] ──→ [PostgreSQL]
[Web Client]     ──┬─HTTP:3000──→ [Web App] ────→ [PostgreSQL]
                   ├─HTTP──────→ [Auth Server] ──→ [PostgreSQL]
                   └─HTTP──────→ [Single API] ──→ [PostgreSQL]

프록시 계층:
[Nginx:443] ──HTTPS──→ 모든 HTTP 서비스들
```

**주요 데이터 플로우:**
- 모든 클라이언트는 OIDC Auth Server를 통해 인증 후 JWT 토큰을 획득
- 데스크톱/모바일 클라이언트는 TCP:9999로 게임 서버와 실시간 통신
- 모바일 클라이언트는 싱글플레이어 모드를 위해 Single API와도 통신
- 웹 클라이언트는 로비/관리 기능만 제공하며 HTTP 통신 사용
- 모든 서버는 PostgreSQL과 직접 연결되어 데이터를 관리

## 8. 환경 변수 설정

### 개발 환경 (.env)

| 키 | 범위 | 예시값 | 필수 | 설명 |
|----|------|--------|------|------|
| `NODE_ENV` | 전역 | `development` | ✅ | 실행 환경 |
| `LOG_LEVEL` | 전역 | `info` | ❌ | 로그 레벨 |
| **데이터베이스** |
| `DATABASE_URL` | 웹/API | `postgresql://user:pass@localhost:5432/db` | ✅ | Prisma 연결 URL |
| `DB_HOST` | 전역 | `localhost` | ✅ | PostgreSQL 호스트 |
| `DB_PORT` | 전역 | `5432` | ❌ | PostgreSQL 포트 |
| `DB_USER` | 전역 | `admin` | ✅ | DB 사용자명 |
| `DB_PASSWORD` | 전역 | `password` | ✅ | DB 비밀번호 |
| **인증** |
| `JWT_SECRET` | 전역 | `your-secret-key` | ✅ | JWT 서명 키 |
| `NEXTAUTH_SECRET` | 웹 | `nextauth-secret` | ✅ | NextAuth.js 세션 키 |
| `GOOGLE_CLIENT_ID` | 인증/웹 | `google-oauth-id` | ✅ | Google OAuth ID |
| `GOOGLE_CLIENT_SECRET` | 인증/웹 | `google-oauth-secret` | ✅ | Google OAuth 시크릿 |

### 프로덕션 환경 (.env.prod)

추가적으로 프로덕션 환경에서는 다음 변수들이 설정됩니다:

| 키 | 설명 | 예시값 |
|----|------|--------|
| `DOMAIN` | 도메인 이름 | `blokus-online.mooo.com` |
| `CERTBOT_EMAIL` | SSL 인증서 관리용 이메일 | `admin@example.com` |
| `WEB_APP_URL` | 웹 애플리케이션 URL | `https://blokus-online.mooo.com` |
| `CLIENT_DOWNLOAD_URL` | 클라이언트 다운로드 URL | `https://blokus-online.mooo.com/downloads` |

### SSH 및 배포 관련

SSH 키 관리 및 자동 배포를 위한 환경 설정:
- GitHub Actions에서 SSH 키를 통한 서버 접근
- `deploy.sh` 스크립트를 통한 자동 배포 프로세스
- Let's Encrypt를 통한 SSL 인증서 자동 갱신

**📝 참고:** 개발 환경에서는 `.env`를, 프로덕션에서는 시크릿 관리 시스템을 통해 환경변수를 관리합니다.

## 9. 테스트 & 품질 보증

### 개발 과정에서의 테스트 접근법

본 프로젝트에서는 잦은 커밋과 단위/통합 테스트 중심의 로컬 테스트를 우선적으로 진행했습니다:

**로컬 테스트 프로세스:**
- 각 기능 구현 후 즉시 단위 테스트 작성
- API 엔드포인트에 대한 통합 테스트 수행
- C++ 코드의 경우 Google Test 프레임워크 활용
- Node.js 서비스는 Jest 기반 테스트 스위트 구성

**CI/CD 파이프라인 통합:**
- 로컬에서 안정화된 코드를 main 브랜치에 merge
- GitHub Actions를 통한 자동화된 빌드 및 테스트 수행
- Docker 컨테이너 환경에서의 통합 테스트 실행

**개발/배포 환경 차이 해결:**
- Docker Compose를 통한 환경 일관성 확보
- 환경별 설정 분리 (.env, .env.prod)
- 배포 과정에서 발생하는 이슈들을 점진적으로 트러블슈팅

## 10. 릴리스 & 버전 관리

**버전 정책:**
- Semantic Versioning (SemVer) 준수
- 태그 형식: `v1.0.0`, `v1.0.0-beta.1`
- CHANGELOG.md에서 변경 사항 추적

**릴리스 프로세스:**
1. 버전 태그 생성 (`git tag v1.0.0`)
2. GitHub Actions가 자동 빌드/패키징
3. [GitHub Releases](https://github.com/zzzz955/blokus-online/releases)에 아티팩트 업로드
4. 서명된 바이너리 배포 (데스크톱/모바일)

## 11. 보안 & 취약점 신고

**취약점 신고:**
- 📧 이메일: zzzzz955@gmail.com
- 🔒 GitHub Security Advisories 사용 권장

**보안 체크리스트:**
- [ ] 의존성 취약점 스캔 (npm audit, Snyk)
- [ ] 시크릿 스캔 (GitHub Advanced Security)
- [ ] 정기 의존성 업데이트 (Dependabot)
- [ ] JWT 토큰 만료 시간 단축 (10분 권장)
- [ ] HTTPS 강제 및 HSTS 헤더 설정

## 12. 기여하기

**버그 신고:**
- GitHub Issues에서 Bug 템플릿 사용을 권장합니다
- 재현 가능한 단계와 환경 정보를 포함해주세요
- 스크린샷이나 로그 파일이 있다면 첨부해주세요

**Pull Request:**
- [ ] 테스트 통과 및 새 테스트 추가
- [ ] 린트/포맷 규칙 준수
- [ ] 문서 업데이트 (API 변경 시)
- [ ] Conventional Commits 준수

## 13. 라이센스

이 프로젝트는 MIT 라이센스 하에 배포됩니다.

## 14. 프로젝트 정보

**개발자:** [zzzz955](https://github.com/zzzz955)

**개발 진행 상황:**
자세한 개발 과정과 진행 상황은 [GitHub Projects](https://github.com/users/zzzz955/projects/1)에서 확인할 수 있습니다.

**감사의 글:**
- Qt, Unity, Next.js 커뮤니티의 오픈소스 라이브러리와 문서 기여자들
- Protocol Buffers, PostgreSQL 등 인프라 기술 개발팀들
- 베타 테스트 및 피드백을 제공해주신 모든 분들

---

🎮 **즐거운 블로블로 게임 되세요!** | **문의:** zzzzz955@gmail.com