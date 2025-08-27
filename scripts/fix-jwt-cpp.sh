#!/bin/bash
# jwt-cpp 문제 해결을 위한 스크립트

set -euo pipefail

echo "=== jwt-cpp 문제 해결 시작 ==="

# vcpkg 환경 변수 확인
if [ -z "${VCPKG_ROOT:-}" ]; then
    echo "ERROR: VCPKG_ROOT not set"
    exit 1
fi

echo "VCPKG_ROOT: ${VCPKG_ROOT}"
echo "VCPKG_DEFAULT_TRIPLET: ${VCPKG_DEFAULT_TRIPLET}"

# 현재 설치된 패키지 확인
echo "=== 현재 설치된 패키지 ==="
"${VCPKG_ROOT}/vcpkg" list | grep jwt || echo "jwt-cpp not found in installed packages"

# jwt-cpp 강제 재설치 시도
echo "=== jwt-cpp 재설치 시도 ==="
"${VCPKG_ROOT}/vcpkg" remove jwt-cpp --triplet="${VCPKG_DEFAULT_TRIPLET}" || echo "jwt-cpp not installed, continuing"

# 캐시 클리어
echo "=== vcpkg 캐시 클리어 ==="
rm -rf "${VCPKG_ROOT}/buildtrees/jwt-cpp" || true
rm -rf "${VCPKG_ROOT}/packages/jwt-cpp_${VCPKG_DEFAULT_TRIPLET}" || true

# 상세 로그와 함께 재설치
echo "=== jwt-cpp 상세 설치 ==="
"${VCPKG_ROOT}/vcpkg" install jwt-cpp --triplet="${VCPKG_DEFAULT_TRIPLET}" \
    --debug \
    --x-buildtrees-root=/tmp/vcpkg-buildtrees \
    --x-install-root="${VCPKG_ROOT}/installed" \
    --x-packages-root=/tmp/vcpkg-packages

# 설치 확인
echo "=== 설치 확인 ==="
"${VCPKG_ROOT}/vcpkg" list | grep jwt || {
    echo "ERROR: jwt-cpp installation failed"
    exit 1
}

# cmake 파일 확인
CMAKE_CONFIG_PATH="${VCPKG_ROOT}/installed/${VCPKG_DEFAULT_TRIPLET}/share/jwt-cpp"
if [ -d "$CMAKE_CONFIG_PATH" ]; then
    echo "=== CMake config files found ==="
    ls -la "$CMAKE_CONFIG_PATH"
else
    echo "ERROR: CMake config files not found at $CMAKE_CONFIG_PATH"
    exit 1
fi

echo "=== jwt-cpp 문제 해결 완료 ==="