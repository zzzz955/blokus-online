#!/bin/bash
# ==================================================
# Docker Build Script - Blokus Online Server
# vcpkg 기반 최적화된 빌드
# ==================================================

set -euo pipefail

# 색상 출력
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
echo_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
echo_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
echo_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# 기본 설정
IMAGE_NAME="blokus-server"
TAG="${1:-latest}"
BUILD_ARGS=""

# Docker Buildkit 활성화 (성능 향상)
export DOCKER_BUILDKIT=1

echo_info "Building Blokus Server Docker image..."
echo_info "Image: ${IMAGE_NAME}:${TAG}"

# 빌드 시작 시간 기록
START_TIME=$(date +%s)

# Docker 빌드 실행 (캐시 최적화)
echo_info "Starting Docker build with cache optimization..."

docker build \
    --tag "${IMAGE_NAME}:${TAG}" \
    --build-arg BUILDKIT_INLINE_CACHE=1 \
    --cache-from "${IMAGE_NAME}:latest" \
    --progress=plain \
    ${BUILD_ARGS} \
    .

# 빌드 결과 확인
if [ $? -eq 0 ]; then
    END_TIME=$(date +%s)
    DURATION=$((END_TIME - START_TIME))
    
    echo_success "Docker build completed successfully!"
    echo_info "Build time: ${DURATION} seconds"
    echo_info "Image size:"
    docker images "${IMAGE_NAME}:${TAG}" --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}"
else
    echo_error "Docker build failed!"
    exit 1
fi

# 빌드 정보 출력
echo_info "Build information:"
docker image inspect "${IMAGE_NAME}:${TAG}" --format '{{json .Config.Labels}}' | jq -r '
    "Build Strategy: " + (."build-strategy" // "unknown") + "\n" +
    "vcpkg Triplet: " + (."vcpkg-triplet" // "unknown") + "\n" +
    "Ubuntu Base: " + (."ubuntu-base" // "unknown")
'

echo_success "Ready to run with: docker run -p 9999:9999 ${IMAGE_NAME}:${TAG}"