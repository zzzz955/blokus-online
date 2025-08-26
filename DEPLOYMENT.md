# Blokus Online - Production Deployment Guide

## 🏗️ Architecture Overview

```
Host Machine
├── 📁 /opt/blokus/thumbnails (host volume)
│
├── 🐳 Web Container (port 3000)
│   ├── Next.js Admin Panel
│   ├── Stage creation/editing
│   ├── SVG thumbnail generation
│   └── 📡 /api/thumbnails/* serving
│
├── 🐳 Single-Player-API Container (port 8080)
│   ├── Node.js REST API
│   ├── Compact format DB queries
│   └── 🔗 Thumbnail URLs → Web Container
│
├── 🐳 Multiplayer Server Container (port 9999)
│   └── C++ TCP Game Server
│
└── 🐳 PostgreSQL Container (port 5432)
    └── Database with compact board states
```

## 🚀 Quick Start

### 1. Environment Setup
```bash
# Copy environment template
cp .env.example .env

# Edit configuration
nano .env
```

### 2. Create Host Directory
```bash
# Create thumbnails directory on host
sudo mkdir -p /opt/blokus/thumbnails
sudo chown -R 1000:1000 /opt/blokus/thumbnails
sudo chmod 755 /opt/blokus/thumbnails

# Update .env
echo "THUMBNAIL_HOST_PATH=/opt/blokus/thumbnails" >> .env
```

### 3. Deploy Services
```bash
# Build and start all services
docker-compose up -d

# Check service status
docker-compose ps

# View logs
docker-compose logs -f
```

## 📝 Configuration

### Environment Variables

**Required:**
```env
DB_PASSWORD=your-secure-password
NEXTAUTH_SECRET=your-32-character-secret
JWT_SECRET=your-jwt-secret
THUMBNAIL_HOST_PATH=/opt/blokus/thumbnails
```

**Production URLs:**
```env
NEXTAUTH_URL=https://admin.yourdomain.com
ALLOWED_ORIGINS=https://admin.yourdomain.com,https://api.yourdomain.com
```

## 🔧 Host Volume Configuration

### Why Host Volumes?
- **Cost Effective**: No cloud storage fees
- **Performance**: Direct filesystem access
- **Persistence**: Survives container restarts
- **Backup**: Standard filesystem backup tools

### Volume Mapping
```yaml
web:
  volumes:
    - ${THUMBNAIL_HOST_PATH}:/app/public/stage-thumbnails
```

### Permissions
```bash
# Ensure correct ownership
sudo chown -R 1000:1000 /opt/blokus/thumbnails

# Set appropriate permissions
sudo chmod -R 755 /opt/blokus/thumbnails
```

## 🌐 Data Flow

### Stage Creation Flow
```
1. Admin creates stage in Web UI
2. Web generates SVG thumbnail → Host volume
3. Web stores compact board state → Database
4. API serves compact data → Client
5. Client requests thumbnail → Web /api/thumbnails
```

### Network Efficiency
```javascript
// Database storage (compact):
{obsIdx: [100, 101], pre: [[5,10,1]]}

// Network transfer: Same compact format
// Client parsing: obsIdx → coordinates
```

## 배포 과정

### 1. 자동 배포 (GitHub Actions)
`main` 브랜치에 푸시하면 자동으로 배포가 실행됩니다.

### 2. 수동 배포
```bash
# 1. 서버에 접속
ssh -p [SSH_PORT] [SSH_USER]@[SSH_HOST]

# 2. 프로젝트 디렉토리로 이동
cd ~/blokus-online

# 3. 최신 코드 받기
git pull origin main

# 4. 환경변수 파일 확인/생성
cp .env.example .env
vi .env  # 필요한 환경변수 설정

# 5. 컨테이너 빌드 및 실행
docker-compose down
docker-compose build --parallel
docker-compose up -d
```

## SSL 인증서 설정

### 초기 설정
```bash
# SSL 인증서 초기 발급
./scripts/init-ssl.sh
```

### 인증서 갱신
```bash
# 수동 갱신
./scripts/renew-ssl.sh

# 강제 갱신
./scripts/renew-ssl.sh --force
```

### 자동 갱신 (크론 설정)
```bash
# 크론탭 편집
crontab -e

# 매월 1일 오전 3시에 인증서 갱신 시도
0 3 1 * * /home/[사용자명]/blokus-online/scripts/renew-ssl.sh >> /var/log/ssl-renewal.log 2>&1
```

## 서비스 관리

### 컨테이너 상태 확인
```bash
docker-compose ps
docker-compose logs [서비스명]
```

### 개별 서비스 재시작
```bash
# 게임 서버 재시작
docker-compose restart blokus-server

# 웹 애플리케이션 재시작
docker-compose restart blokus-web

# Nginx 재시작
docker-compose restart nginx

# 전체 재시작
docker-compose restart
```

### 로그 확인
```bash
# 전체 로그
docker-compose logs -f

# 개별 서비스 로그
docker-compose logs -f blokus-server
docker-compose logs -f blokus-web
docker-compose logs -f nginx

# 실시간 로그 모니터링
tail -f logs/nginx/access.log
tail -f logs/nginx/error.log
```

## 클라이언트 다운로드 파일 관리

### 파일 업로드
```bash
# 서버의 downloads 디렉토리에 파일 업로드
scp -P [SSH_PORT] BlokusClient-v1.0.0.zip [SSH_USER]@[SSH_HOST]:~/blokus-online/downloads/

# 최신 버전 심볼릭 링크 생성
ssh -p [SSH_PORT] [SSH_USER]@[SSH_HOST]
cd ~/blokus-online/downloads
ln -sf BlokusClient-v1.0.0.zip BlokusClient-latest.zip
```

### 다운로드 URL
- 최신 버전: `https://blokus-online.mooo.com/downloads/BlokusClient-latest.zip`
- 특정 버전: `https://blokus-online.mooo.com/downloads/BlokusClient-v1.0.0.zip`

## 데이터베이스 관리

### 백업
```bash
# PostgreSQL 백업
docker-compose exec postgres pg_dump -U [DB_USER] [DB_NAME] > backup_$(date +%Y%m%d_%H%M%S).sql

# 웹 데이터베이스 백업 (외부 PostgreSQL 서버)
pg_dump -h [WEB_DB_HOST] -p [WEB_DB_PORT] -U [WEB_DB_USER] [WEB_DB_NAME] > web_backup_$(date +%Y%m%d_%H%M%S).sql
```

### 복원
```bash
# 게임 데이터베이스 복원
docker-compose exec -T postgres psql -U [DB_USER] [DB_NAME] < backup_20240131_120000.sql

# 웹 데이터베이스 복원
psql -h [WEB_DB_HOST] -p [WEB_DB_PORT] -U [WEB_DB_USER] [WEB_DB_NAME] < web_backup_20240131_120000.sql
```

## 모니터링

### 시스템 상태 확인
```bash
# 컨테이너 리소스 사용량
docker stats

# 디스크 사용량
df -h

# 메모리 사용량
free -h

# 네트워크 포트 확인
netstat -tlnp | grep -E "(80|443|3000|9999|5432)"
```

### 헬스체크 엔드포인트
- 웹 애플리케이션: `https://blokus-online.mooo.com/api/health`
- Nginx 상태: `https://blokus-online.mooo.com/health`

## 트러블슈팅

### 일반적인 문제들

#### 1. 컨테이너가 시작되지 않는 경우
```bash
# 로그 확인
docker-compose logs [서비스명]

# 개별 컨테이너 실행 테스트
docker run --rm -it [이미지명] /bin/sh
```

#### 2. 웹사이트에 접속되지 않는 경우
```bash
# Nginx 설정 테스트
docker-compose exec nginx nginx -t

# 포트 확인
netstat -tlnp | grep -E "(80|443)"

# SSL 인증서 확인
openssl x509 -in ./certbot/conf/live/blokus-online.mooo.com/fullchain.pem -text -noout
```

#### 3. 게임 서버 연결 실패
```bash
# 게임 서버 포트 확인
netstat -tlnp | grep 9999

# 방화벽 상태 확인
sudo ufw status

# 컨테이너 간 네트워크 확인
docker network ls
docker network inspect blokus-network
```

#### 4. 데이터베이스 연결 실패
```bash
# PostgreSQL 상태 확인
docker-compose exec postgres pg_isready

# 데이터베이스 연결 테스트
docker-compose exec postgres psql -U [DB_USER] -d [DB_NAME] -c "SELECT 1;"
```

### 로그 위치
- Nginx: `./logs/nginx/`
- 애플리케이션: `./logs/`
- Docker 컨테이너 로그: `docker-compose logs`

## 보안 고려사항

### 방화벽 설정
```bash
# UFW 방화벽 설정
sudo ufw allow 22/tcp      # SSH
sudo ufw allow 80/tcp      # HTTP
sudo ufw allow 443/tcp     # HTTPS  
sudo ufw allow 9999/tcp    # 게임 서버
sudo ufw enable
```

### 정기 업데이트
```bash
# 시스템 패키지 업데이트
sudo apt update && sudo apt upgrade -y

# Docker 이미지 업데이트
docker-compose pull
docker-compose up -d
```

### 백업 스케줄링
```bash
# 일일 백업 크론 작업
0 2 * * * /home/[사용자명]/blokus-online/scripts/backup.sh >> /var/log/backup.log 2>&1
```

## 성능 최적화

### Docker 리소스 제한
```yaml
# docker-compose.yml에서 리소스 제한 설정
services:
  blokus-server:
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: '0.5'
```

### Nginx 캐싱 설정
정적 파일 캐싱은 이미 설정되어 있으며, 추가 최적화가 필요한 경우 `nginx/nginx.conf`를 수정하세요.

## 연락처 및 지원
- 기술 문의: [기술팀 이메일]
- 긴급 상황: [긴급 연락처]
- 문서 업데이트: [문서 관리자]