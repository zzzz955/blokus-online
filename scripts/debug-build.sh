#!/bin/bash

# ==================================================
# Docker 빌드 디버깅 스크립트
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

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# 빌드 디버깅 함수
debug_build() {
    log_step "Docker 빌드 디버깅 시작..."
    
    # 로컬에서 CMake 테스트
    log_info "로컬 CMake 설정 테스트 중..."
    
    if [ -d "build" ]; then
        rm -rf build
    fi
    
    mkdir -p build
    cd build
    
    echo "=== 사용 가능한 CMake 옵션 확인 ==="
    cmake .. --help | grep -A 5 -B 5 "CMAKE_BUILD_TYPE" || true
    
    echo "=== CMake 설정 시도 ==="
    cmake .. \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=17 \
        -DCMAKE_VERBOSE_MAKEFILE=ON || {
        log_error "CMake 설정 실패"
        return 1
    }
    
    echo "=== 빌드 타겟 확인 ==="
    make help | head -20
    
    echo "=== Makefile 확인 ==="
    ls -la Makefile
    
    cd ..
}

# Docker 빌드 테스트
test_docker_build() {
    log_step "Docker 빌드 테스트 중..."
    
    # 단계별 빌드 테스트
    log_info "1. 기본 Dockerfile 테스트"
    docker build --target builder -t blokus-debug:builder . || {
        log_error "Builder 단계 실패"
        
        log_info "2. Fallback Dockerfile 테스트"
        docker build -f Dockerfile.fallback -t blokus-debug:fallback . || {
            log_error "Fallback 빌드도 실패"
            return 1
        }
    }
    
    log_info "빌드 성공!"
}

# 로그 분석
analyze_logs() {
    log_step "빌드 로그 분석 중..."
    
    # 최근 Docker 빌드 로그 확인
    docker system events --since 10m --filter type=container || true
}

# 정리
cleanup() {
    log_step "정리 중..."
    
    # 빌드 디렉토리 정리
    if [ -d "build" ]; then
        rm -rf build
    fi
    
    # Docker 이미지 정리
    docker image prune -f || true
}

# 메인 함수
main() {
    echo "=== Blokus Docker 빌드 디버깅 ==="
    echo
    
    case "${1:-all}" in
        "local")
            debug_build
            ;;
        "docker")
            test_docker_build
            ;;
        "logs")
            analyze_logs
            ;;
        "clean")
            cleanup
            ;;
        "all"|*)
            debug_build
            test_docker_build
            cleanup
            ;;
    esac
}

# 스크립트 실행
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi