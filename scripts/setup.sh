#!/bin/bash

# ==================================================
# Blokus Online 서버 초기 설정 스크립트
# ==================================================

set -e

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Docker 설치 체크 및 설치
install_docker() {
    log_step "Docker 설치 상태 확인 중..."
    
    if command -v docker &> /dev/null; then
        log_info "Docker가 이미 설치되어 있습니다."
        docker --version
    else
        log_info "Docker 설치 중..."
        
        # 필요한 패키지 설치
        sudo apt-get update
        sudo apt-get install -y \
            ca-certificates \
            curl \
            gnupg \
            lsb-release
        
        # Docker GPG 키 추가
        sudo mkdir -p /etc/apt/keyrings
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
        
        # Docker repository 추가
        echo \
          "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
          $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
        
        # Docker 설치
        sudo apt-get update
        sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
        
        log_info "Docker 설치 완료"
    fi
    
    # 현재 사용자를 docker 그룹에 추가
    if ! groups | grep -q docker; then
        log_info "사용자를 docker 그룹에 추가 중..."
        sudo usermod -aG docker $USER
        log_warn "Docker 그룹 변경사항을 적용하려면 로그아웃 후 다시 로그인하세요."
    fi
}

# Docker Compose 설치
install_docker_compose() {
    log_step "Docker Compose 설치 상태 확인 중..."
    
    if command -v docker-compose &> /dev/null; then
        log_info "Docker Compose가 이미 설치되어 있습니다."
        docker-compose --version
    else
        log_info "Docker Compose 설치 중..."
        
        # 최신 버전 가져오기
        COMPOSE_VERSION=$(curl -s https://api.github.com/repos/docker/compose/releases/latest | grep -Po '"tag_name": "\K.*\d')
        
        # Docker Compose 다운로드 및 설치
        sudo curl -L "https://github.com/docker/compose/releases/download/${COMPOSE_VERSION}/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
        sudo chmod +x /usr/local/bin/docker-compose
        
        log_info "Docker Compose 설치 완료"
    fi
}

# 필수 디렉토리 생성
create_directories() {
    log_step "필수 디렉토리 생성 중..."
    
    local dirs=(
        "logs"
        "backups"
        "config"
        "sql"
    )
    
    for dir in "${dirs[@]}"; do
        if [ ! -d "$dir" ]; then
            mkdir -p "$dir"
            log_info "디렉토리 생성: $dir"
        fi
    done
    
    # 권한 설정
    chmod 755 logs backups config sql
}

# 환경변수 파일 생성
create_env_file() {
    log_step "환경변수 파일 생성 중..."
    
    if [ -f ".env" ]; then
        log_warn ".env 파일이 이미 존재합니다. 백업 후 새로 생성하시겠습니까? (y/N)"
        read -r response
        if [[ "$response" =~ ^[Yy]$ ]]; then
            cp .env .env.backup.$(date +%Y%m%d_%H%M%S)
            log_info "기존 .env 파일을 백업했습니다."
        else
            log_info ".env 파일 생성을 건너뜁니다."
            return
        fi
    fi
    
    # JWT 시크릿 생성
    JWT_SECRET=$(openssl rand -hex 32)
    DB_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-25)
    
    cat > .env << EOF
# ==================================================
# Blokus Online 서버 환경변수 설정
# ==================================================

# 서버 설정
SERVER_PORT=9999
SERVER_MAX_CLIENTS=1000  
SERVER_THREAD_POOL_SIZE=4

# 데이터베이스 설정
DB_HOST=postgres
DB_PORT=5432
DB_USER=blokus_admin
DB_PASSWORD=${DB_PASSWORD}
DB_NAME=blokus_online
DB_POOL_SIZE=10

# PostgreSQL 설정 (Docker Compose용)
POSTGRES_DB=blokus_online
POSTGRES_USER=blokus_admin
POSTGRES_PASSWORD=${DB_PASSWORD}

# 보안 설정
JWT_SECRET=${JWT_SECRET}
SESSION_TIMEOUT_HOURS=24
PASSWORD_SALT_ROUNDS=12

# 로깅 설정
LOG_LEVEL=info
LOG_DIRECTORY=/app/logs

# 개발 설정
DEBUG_MODE=false
ENABLE_SQL_LOGGING=false
EOF
    
    chmod 600 .env
    log_info ".env 파일이 생성되었습니다. 필요시 설정을 수정하세요."
}

# 초기 SQL 스크립트 생성
create_init_sql() {
    log_step "초기 SQL 스크립트 생성 중..."
    
    if [ ! -f "sql/init.sql" ]; then
        cat > sql/init.sql << 'EOF'
-- ==================================================
-- Blokus Online 데이터베이스 초기화 스크립트
-- ==================================================

-- UTF-8 인코딩 설정
SET client_encoding = 'UTF8';

-- 사용자 테이블
CREATE TABLE IF NOT EXISTS users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT true
);

-- 사용자 통계 테이블
CREATE TABLE IF NOT EXISTS user_stats (
    user_id INTEGER PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    total_games INTEGER DEFAULT 0,
    wins INTEGER DEFAULT 0,
    losses INTEGER DEFAULT 0,
    level INTEGER DEFAULT 1,
    experience_points INTEGER DEFAULT 0,
    total_score INTEGER DEFAULT 0,
    best_score INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 게임 세션 테이블 (옵션)
CREATE TABLE IF NOT EXISTS game_sessions (
    session_id SERIAL PRIMARY KEY,
    room_id VARCHAR(50) NOT NULL,
    players JSONB NOT NULL,
    start_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    end_time TIMESTAMP,
    winner_id INTEGER REFERENCES users(user_id),
    game_data JSONB,
    status VARCHAR(20) DEFAULT 'active'
);

-- 인덱스 생성
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
CREATE INDEX IF NOT EXISTS idx_users_active ON users(is_active);
CREATE INDEX IF NOT EXISTS idx_game_sessions_room ON game_sessions(room_id);
CREATE INDEX IF NOT EXISTS idx_game_sessions_status ON game_sessions(status);

-- 기본 데이터 삽입 (테스트용)
INSERT INTO users (username, password_hash) VALUES 
('admin', '$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj3OVBND/KSy') -- 비밀번호: admin123
ON CONFLICT (username) DO NOTHING;

-- 트리거 함수 (updated_at 자동 업데이트)
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- 트리거 적용
DROP TRIGGER IF EXISTS update_users_updated_at ON users;
CREATE TRIGGER update_users_updated_at 
    BEFORE UPDATE ON users 
    FOR EACH ROW 
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_user_stats_updated_at ON user_stats;
CREATE TRIGGER update_user_stats_updated_at 
    BEFORE UPDATE ON user_stats 
    FOR EACH ROW 
    EXECUTE FUNCTION update_updated_at_column();

-- 완료 메시지
SELECT 'Blokus Online 데이터베이스 초기화 완료!' as message;
EOF
        
        log_info "초기 SQL 스크립트가 생성되었습니다: sql/init.sql"
    else
        log_info "초기 SQL 스크립트가 이미 존재합니다."
    fi
}

# 방화벽 설정
configure_firewall() {
    log_step "방화벽 설정 확인 중..."
    
    if command -v ufw &> /dev/null; then
        # 서버 포트 열기
        local server_port=${SERVER_PORT:-9999}
        
        log_info "서버 포트 $server_port 허용 중..."
        sudo ufw allow $server_port/tcp
        
        # SSH 포트 확인
        if ! sudo ufw status | grep -q "22/tcp"; then
            log_warn "SSH 포트 22를 허용하시겠습니까? (Y/n)"
            read -r response
            if [[ ! "$response" =~ ^[Nn]$ ]]; then
                sudo ufw allow 22/tcp
            fi
        fi
        
        # 방화벽 상태 출력
        sudo ufw status
    else
        log_warn "ufw가 설치되지 않았습니다. 수동으로 방화벽을 설정하세요."
    fi
}

# 시스템 서비스 설정 (옵션)
create_systemd_service() {
    log_step "시스템 서비스 생성 (옵션)..."
    
    echo "부팅시 자동 시작 서비스를 생성하시겠습니까? (y/N)"
    read -r response
    
    if [[ "$response" =~ ^[Yy]$ ]]; then
        local service_path="/etc/systemd/system/blokus-online.service"
        local project_path=$(pwd)
        
        sudo tee $service_path > /dev/null << EOF
[Unit]
Description=Blokus Online Game Server
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=${project_path}
ExecStart=/usr/local/bin/docker-compose up -d
ExecStop=/usr/local/bin/docker-compose down
TimeoutStartSec=0
User=$(whoami)

[Install]
WantedBy=multi-user.target
EOF
        
        sudo systemctl daemon-reload
        sudo systemctl enable blokus-online.service
        
        log_info "시스템 서비스가 생성되었습니다."
        log_info "서비스 제어: sudo systemctl {start|stop|status} blokus-online"
    fi
}

# 설정 완료 메시지
setup_complete() {
    echo
    log_info "🎉 Blokus Online 서버 초기 설정 완료!"
    echo
    echo "=== 다음 단계 ==="
    echo "1. .env 파일에서 설정 확인 및 수정"
    echo "2. 배포 실행: ./scripts/deploy.sh"
    echo "3. 로그 확인: docker-compose logs -f"
    echo
    echo "=== 유용한 명령어 ==="
    echo "• 수동 배포: ./scripts/deploy.sh"
    echo "• 서비스 상태: docker-compose ps"
    echo "• 컨테이너 중지: docker-compose down"
    echo "• 로그 확인: docker-compose logs -f blokus-server"
    echo
}

# 메인 함수
main() {
    echo "=== Blokus Online 서버 초기 설정 ==="
    echo
    
    # 사전 요구사항 확인
    if [[ $EUID -eq 0 ]]; then
        log_error "root 사용자로 실행하지 마세요!"
        exit 1
    fi
    
    # 설정 단계 실행
    install_docker
    install_docker_compose
    create_directories
    create_env_file
    create_init_sql
    configure_firewall
    create_systemd_service
    
    setup_complete
}

# 스크립트 실행
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi