# Blokus Online - CI/CD 배포 가이드

## 개요

이 문서는 Blokus Online 게임 서버의 CI/CD 파이프라인을 이용한 자동 배포 가이드입니다.

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   GitHub Repo   │───▶│ GitHub Actions  │───▶│  Ubuntu Server  │
│                 │    │                 │    │                 │
│ - Source Code   │    │ - Build Docker  │    │ - C++ Server    │
│ - Workflows     │    │ - Deploy via SSH│    │ - PostgreSQL    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## 1. 서버 초기 설정

### 1.1 서버 환경 요구사항
- Ubuntu 20.04 LTS 이상
- Docker 및 Docker Compose
- SSH 키 기반 인증 설정
- 방화벽 포트 개방 (9999번 포트)

### 1.2 초기 설정 스크립트 실행

```bash
# 저장소 클론
git clone https://github.com/your-username/blokus-online.git
cd blokus-online

# 스크립트 실행 권한 부여
chmod +x scripts/*.sh

# 초기 서버 환경 설정
./scripts/setup.sh
```

이 스크립트는 다음 작업을 수행합니다:
- Docker 및 Docker Compose 설치
- 필요한 디렉토리 생성 (logs, backups, config, sql)
- 환경변수 파일(.env) 생성
- 데이터베이스 초기화 스크립트 생성
- 방화벽 설정
- 시스템 서비스 등록 (옵션)

## 2. GitHub Repository 설정

### 2.1 Repository Secrets 설정

GitHub 저장소의 Settings > Secrets and variables > Actions에서 다음 환경변수를 설정하세요:

#### SSH 연결 설정
```
SSH_HOST=your-server-domain-or-ip
SSH_USER=your-username
SSH_PORT=22
SSH_KEY=your-private-key-content
```

#### 서버 설정
```
BLOKUS_SERVER_PORT=9999
SERVER_MAX_CLIENTS=1000
SERVER_THREAD_POOL_SIZE=4
```

#### 데이터베이스 설정
```
DB_PORT=5432
DB_USER=blokus_admin
DB_PASSWORD=your-secure-password
DB_NAME=blokus_online
DB_POOL_SIZE=10
```

#### 보안 설정
```
JWT_SECRET=your-jwt-secret-key
SESSION_TIMEOUT_HOURS=24
PASSWORD_SALT_ROUNDS=12
```

#### 로깅 설정
```
LOG_LEVEL=info
LOG_DIRECTORY=/app/logs
```

#### 개발 설정
```
DEBUG_MODE=false
ENABLE_SQL_LOGGING=false
```

### 2.2 SSH 키 생성 및 설정

```bash
# 서버에서 SSH 키 생성
ssh-keygen -t rsa -b 4096 -C "github-actions@your-domain.com"

# 공개키를 authorized_keys에 추가
cat ~/.ssh/id_rsa.pub >> ~/.ssh/authorized_keys

# 개인키 내용을 Repository Secrets의 SSH_PRIVATE_KEY에 설정
cat ~/.ssh/id_rsa
```

## 3. 자동 배포 (CI/CD)

### 3.1 배포 트리거
- `main` 브랜치에 코드 Push 시 자동 배포
- GitHub Actions 탭에서 수동 실행 가능

### 3.2 배포 과정
1. **코드 체크아웃**: 최신 소스코드 가져오기
2. **SSH 연결 설정**: 서버에 안전한 연결 설정
3. **소스코드 배포**: Git을 통한 코드 업데이트
4. **환경변수 주입**: Repository Secrets에서 .env 파일 생성
5. **컨테이너 재배포**: 기존 컨테이너 정리 후 새 이미지 빌드/실행
6. **헬스체크**: 서비스 정상 동작 확인

### 3.3 배포 상태 확인

배포 완료 후 다음 명령어로 상태를 확인할 수 있습니다:

```bash
# 컨테이너 상태 확인
docker-compose ps

# 로그 확인
docker-compose logs -f blokus-server

# 포트 확인
netstat -tlnp | grep :9999
```

## 4. 수동 배포

GitHub Actions 대신 수동으로 배포하려면:

```bash
# 배포 스크립트 실행
./scripts/deploy.sh
```

이 스크립트는 다음 작업을 수행합니다:
- 환경변수 및 Docker 환경 체크
- 현재 데이터베이스 백업 (옵션)
- 기존 컨테이너 중지 및 정리
- 새 이미지 빌드 및 컨테이너 시작
- 서비스 상태 확인 및 헬스체크
- 실패 시 롤백 옵션 제공

## 5. 운영 관리

### 5.1 로그 관리

```bash
# 실시간 로그 확인
docker-compose logs -f blokus-server

# 최근 로그 확인
docker-compose logs --tail=100 blokus-server

# PostgreSQL 로그 확인
docker-compose logs postgres
```

### 5.2 데이터베이스 백업/복원

```bash
# 백업 생성
docker-compose exec postgres pg_dump -U blokus_admin blokus_online > backup.sql

# 백업 복원
docker-compose exec -T postgres psql -U blokus_admin blokus_online < backup.sql
```

### 5.3 서비스 제어

```bash
# 서비스 시작
docker-compose up -d

# 서비스 중지
docker-compose down

# 서비스 재시작
docker-compose restart

# 특정 서비스만 재시작
docker-compose restart blokus-server
```

### 5.4 시스템 서비스 (설정한 경우)

```bash
# 서비스 시작
sudo systemctl start blokus-online

# 서비스 중지
sudo systemctl stop blokus-online

# 서비스 상태 확인
sudo systemctl status blokus-online
```

## 6. 모니터링

### 6.1 헬스체크 엔드포인트
- 서버 포트: `http://your-server:9999`
- PostgreSQL: 컨테이너 내부에서 `pg_isready` 명령어

### 6.2 리소스 모니터링

```bash
# Docker 리소스 사용량
docker stats

# 시스템 리소스
htop
df -h
```

## 7. 문제 해결

### 7.1 일반적인 문제들

**컨테이너가 시작되지 않는 경우:**
```bash
# 로그 확인
docker-compose logs blokus-server

# 환경변수 확인
cat .env

# 포트 충돌 확인
netstat -tlnp | grep :9999
```

**데이터베이스 연결 실패:**
```bash
# PostgreSQL 컨테이너 상태 확인
docker-compose ps postgres

# 데이터베이스 연결 테스트
docker-compose exec postgres pg_isready -U blokus_admin -d blokus_online
```

**빌드 실패:**
```bash
# Docker 이미지 정리
docker system prune -f

# 캐시 없이 재빌드
docker-compose build --no-cache
```

### 7.2 롤백 절차

배포 실패 시 이전 상태로 롤백:

```bash
# 수동 롤백
docker-compose down
# 백업에서 데이터베이스 복원
# 이전 버전 체크아웃 후 재배포
```

## 8. 보안 고려사항

1. **환경변수 관리**: 민감한 정보는 반드시 Repository Secrets 사용
2. **SSH 키 관리**: 정기적인 키 교체, 최소 권한 원칙
3. **방화벽 설정**: 필요한 포트만 개방
4. **HTTPS 사용**: 프로덕션 환경에서는 리버스 프록시 설정
5. **정기 업데이트**: 보안 패치 적용

## 9. 성능 최적화

1. **멀티스테이지 빌드**: Docker 이미지 크기 최소화
2. **레이어 캐싱**: Dockerfile 순서 최적화
3. **리소스 제한**: docker-compose.yml에 CPU/메모리 제한 설정
4. **로그 로테이션**: 로그 파일 크기 관리

---

## 참고 자료

- [Docker 공식 문서](https://docs.docker.com/)
- [Docker Compose 가이드](https://docs.docker.com/compose/)
- [GitHub Actions 문서](https://docs.github.com/en/actions)
- [PostgreSQL 문서](https://www.postgresql.org/docs/)

## 지원

문제가 발생하면 다음을 확인하세요:
1. GitHub Actions 로그
2. 서버의 docker-compose 로그
3. 시스템 로그 (`/var/log/syslog`)
4. 방화벽 설정 (`ufw status`)

추가 지원이 필요한 경우 GitHub Issues를 생성해주세요.