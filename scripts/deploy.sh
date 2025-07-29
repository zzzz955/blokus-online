#!/bin/bash

# ==================================================
# Blokus Online 수동 배포 스크립트
# ==================================================

set -e  # 오류 발생시 스크립트 중단

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 로그 함수
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

# 환경 변수 체크
check_env() {
    log_step "환경 변수 확인 중..."
    
    if [ ! -f ".env" ]; then
        log_error ".env 파일이 존재하지 않습니다!"
        exit 1
    fi
    
    source .env
    
    local required_vars=(
        "DB_USER"
        "DB_PASSWORD" 
        "DB_NAME"
        "JWT_SECRET"
    )
    
    for var in "${required_vars[@]}"; do
        if [ -z "${!var}" ]; then
            log_error "필수 환경변수 '$var'가 설정되지 않았습니다!"
            exit 1
        fi
    done
    
    log_info "환경 변수 체크 완료"
}

# Docker 및 Docker Compose 체크
check_docker() {
    log_step "Docker 환경 확인 중..."
    
    if ! command -v docker &> /dev/null; then
        log_error "Docker가 설치되지 않았습니다!"
        exit 1
    fi
    
    if ! command -v docker-compose &> /dev/null; then
        log_error "Docker Compose가 설치되지 않았습니다!"
        exit 1
    fi
    
    if ! docker info &> /dev/null; then
        log_error "Docker 데몬이 실행되지 않았습니다!"
        exit 1
    fi
    
    log_info "Docker 환경 확인 완료"
}

# 기존 컨테이너 백업
backup_current() {
    log_step "현재 실행 중인 서비스 백업 중..."
    
    # 데이터베이스 백업
    if docker-compose ps postgres | grep -q "Up"; then
        log_info "PostgreSQL 데이터베이스 백업 중..."
        
        mkdir -p backups
        backup_file="backups/blokus_backup_$(date +%Y%m%d_%H%M%S).sql"
        
        docker-compose exec -T postgres pg_dump -U ${DB_USER} ${DB_NAME} > "$backup_file"
        
        if [ $? -eq 0 ]; then
            log_info "데이터베이스 백업 완료: $backup_file"
        else
            log_warn "데이터베이스 백업 실패"
        fi
    fi
}

# 컨테이너 중지 및 정리
stop_services() {
    log_step "기존 서비스 중지 중..."
    
    docker-compose down --remove-orphans
    
    # 사용하지 않는 이미지 정리
    docker image prune -f
    
    log_info "서비스 중지 완료"
}

# 새 컨테이너 빌드 및 시작
start_services() {
    log_step "새 컨테이너 빌드 및 시작 중..."
    
    # 이미지 빌드 (캐시 없이)
    docker-compose build --no-cache blokus-server
    
    # 컨테이너 시작
    docker-compose up -d
    
    log_info "서비스 시작 완료"
}

# 서비스 상태 확인
check_services() {
    log_step "서비스 상태 확인 중..."
    
    # 잠시 대기 (컨테이너 시작 시간)
    sleep 15
    
    # 컨테이너 상태 확인
    if ! docker-compose ps | grep -q "Up"; then
        log_error "일부 서비스가 정상적으로 시작되지 않았습니다!"
        docker-compose logs --tail=50
        exit 1
    fi
    
    # PostgreSQL 연결 테스트
    log_info "PostgreSQL 연결 테스트 중..."
    docker-compose exec -T postgres pg_isready -U ${DB_USER} -d ${DB_NAME}
    
    # 서버 포트 확인
    log_info "서버 포트 확인 중..."
    sleep 5
    
    if netstat -tlnp | grep -q ":${SERVER_PORT:-9999}"; then
        log_info "서버 포트 ${SERVER_PORT:-9999} 정상 동작 중"
    else
        log_warn "서버 포트가 아직 열리지 않았습니다. 로그를 확인하세요."
    fi
    
    log_info "서비스 상태 확인 완료"
}

# 로그 출력
show_logs() {
    log_step "최근 로그 출력..."
    echo
    docker-compose logs --tail=20 blokus-server
    echo
    docker-compose logs --tail=10 postgres
}

# 배포 완료 메시지
deployment_summary() {
    echo
    log_info "🎉 배포 완료!"
    echo
    echo "=== 서비스 정보 ==="
    echo "서버 주소: localhost:${SERVER_PORT:-9999}"
    echo "데이터베이스: PostgreSQL (포트 ${DB_PORT:-5432})"
    echo
    echo "=== 유용한 명령어 ==="
    echo "• 로그 확인: docker-compose logs -f blokus-server"
    echo "• 상태 확인: docker-compose ps"
    echo "• 서비스 중지: docker-compose down"
    echo "• 서비스 재시작: docker-compose restart"
    echo
}

# 롤백 함수
rollback() {
    log_error "배포 실패! 롤백을 수행하시겠습니까? (y/N)"
    read -r response
    
    if [[ "$response" =~ ^[Yy]$ ]]; then
        log_step "롤백 수행 중..."
        
        # 현재 컨테이너 중지
        docker-compose down --remove-orphans
        
        # 최신 백업 복구 (옵션)
        if [ -d "backups" ] && [ "$(ls -A backups/)" ]; then
            latest_backup=$(ls -t backups/*.sql | head -n1)
            log_info "최신 백업 복구 중: $latest_backup"
            
            docker-compose up -d postgres
            sleep 10
            
            docker-compose exec -T postgres psql -U ${DB_USER} -d ${DB_NAME} < "$latest_backup"
        fi
        
        log_info "롤백 완료"
    fi
}

# 메인 함수
main() {
    echo "=== Blokus Online 배포 스크립트 ==="
    echo
    
    # 환경 체크
    check_env
    check_docker
    
    # 백업 (선택사항)
    echo "현재 데이터를 백업하시겠습니까? (Y/n)"
    read -r backup_response
    if [[ ! "$backup_response" =~ ^[Nn]$ ]]; then
        backup_current
    fi
    
    # 배포 수행
    trap rollback ERR
    
    stop_services
    start_services
    check_services
    show_logs
    deployment_summary
    
    trap - ERR
}

# 스크립트 실행
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi