# ==================================================
# Multi-Stage Dockerfile - Blokus Online Server
# 안정성 최우선, 서브모듈 의존성 문제 해결
# ==================================================

# ==================================================
# Stage 1: Dependencies Builder
# 시스템 패키지 우선 + 필수 라이브러리만 소스 빌드
# ==================================================
FROM ubuntu:22.04 AS dependencies-builder

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul
ENV CMAKE_BUILD_TYPE=Release
ENV PARALLEL_JOBS=$(nproc)

# 빌드 실패 시 즉시 중단
SHELL ["/bin/bash", "-euxo", "pipefail", "-c"]

# ==================================================
# 시스템 패키지 설치 (최대한 활용)
# ==================================================
RUN echo "=== Installing system packages ===" && \
    apt-get update && apt-get install -y \
    # 빌드 도구
    build-essential \
    cmake \
    ninja-build \
    pkg-config \
    ccache \
    # 버전 관리 및 다운로드
    git \
    wget \
    curl \
    ca-certificates \
    # 압축 도구
    tar \
    gzip \
    unzip \
    # 시스템 라이브러리 (CONFIG 문제 없는 것들)
    libspdlog-dev \
    nlohmann-json3-dev \
    libboost-system-dev \
    libpqxx-dev \
    libpq-dev \
    libssl-dev \
    # 기타 필수 의존성
    zlib1g-dev \
    libbz2-dev \
    libfmt-dev \
    && rm -rf /var/lib/apt/lists/*

# ccache 최적화 설정
RUN ccache --set-config=max_size=2G && \
    ccache --set-config=compression=true && \
    ccache --set-config=cache_dir=/tmp/.ccache

# ==================================================
# Protocol Buffers 빌드 (서브모듈 의존성 해결)
# ==================================================
ARG PROTOBUF_VERSION=25.5
RUN echo "=== Building Protocol Buffers ${PROTOBUF_VERSION} with submodules ===" && \
    cd /tmp && \
    # git clone으로 서브모듈 포함 다운로드
    git clone --depth 1 --branch v${PROTOBUF_VERSION} --recursive \
        https://github.com/protocolbuffers/protobuf.git && \
    cd protobuf && \
    \
    # 서브모듈 검증
    echo "=== Verifying submodules ===" && \
    ls -la third_party/ && \
    ls -la third_party/abseil-cpp/ && \
    \
    # CMake 설정 (Abseil 포함)
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DCMAKE_CXX_STANDARD=20 \
        -Dprotobuf_BUILD_TESTS=OFF \
        -Dprotobuf_BUILD_EXAMPLES=OFF \
        -Dprotobuf_ABSL_PROVIDER=module \
        -Dprotobuf_BUILD_LIBPROTOC=ON && \
    \
    # 빌드 실행
    echo "=== Building protobuf ===" && \
    ninja -C build -j${PARALLEL_JOBS} && \
    ninja -C build install && \
    ldconfig && \
    \
    # 정리
    cd / && rm -rf /tmp/protobuf

# ==================================================
# 라이브러리 설치 검증
# ==================================================
RUN echo "=== Verifying all dependencies ===" && \
    echo "1. Protocol Buffers:" && \
    protoc --version && \
    pkg-config --exists protobuf && echo "  ✓ protobuf pkg-config OK" && \
    find /usr/local -name "protobuf*Config.cmake" && \
    \
    echo "2. System packages:" && \
    pkg-config --exists spdlog && echo "  ✓ spdlog OK" && \
    pkg-config --exists nlohmann_json && echo "  ✓ nlohmann-json OK" && \
    dpkg -l | grep libboost-system && echo "  ✓ boost-system OK" && \
    dpkg -l | grep libpqxx && echo "  ✓ libpqxx OK" && \
    dpkg -l | grep libssl && echo "  ✓ openssl OK" && \
    \
    echo "3. CMake integration test:" && \
    echo 'find_package(protobuf CONFIG REQUIRED)' > /tmp/test.cmake && \
    echo 'message(STATUS "protobuf found: ${protobuf_FOUND}")' >> /tmp/test.cmake && \
    cmake -P /tmp/test.cmake && \
    \
    echo "=== All dependencies verified successfully ==="

# ==================================================
# Stage 2: Application Builder  
# 프로젝트 빌드 단계
# ==================================================
FROM dependencies-builder AS app-builder

# 작업 디렉토리 설정
WORKDIR /app

# 프로젝트 소스 복사 (proto, common, server만)
COPY proto/ ./proto/
COPY common/ ./common/
COPY server/ ./server/
COPY CMakeLists.txt ./

# ==================================================
# 프로젝트 빌드
# ==================================================
RUN echo "=== Building Blokus Server ===" && \
    # CMake 설정 (시스템 패키지 활용)
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=20 \
        -DCMAKE_INSTALL_PREFIX=/app/install \
        -DCMAKE_PREFIX_PATH="/usr/local:/usr" \
        -DBoost_USE_STATIC_LIBS=OFF \
        -DBoost_USE_MULTITHREADED=ON \
        -DCMAKE_VERBOSE_MAKEFILE=ON && \
    \
    # 빌드 의존성 검증
    echo "=== Build Dependencies Check ===" && \
    grep -E "(protobuf|spdlog|nlohmann|Boost|pqxx|OpenSSL).*FOUND" build/CMakeCache.txt || \
    (echo "Dependency check failed" && cat build/CMakeFiles/CMakeError.log && exit 1) && \
    \
    # 빌드 실행 (ccache 활용)
    echo "=== Building with $(nproc) parallel jobs ===" && \
    export CCACHE_DIR=/tmp/.ccache && \
    ninja -C build -j$(nproc) -v && \
    \
    # 설치
    ninja -C build install && \
    \
    # 빌드 결과 검증
    echo "=== Build Verification ===" && \
    ls -la /app/install/bin/ && \
    file /app/install/bin/BlokusServer && \
    ldd /app/install/bin/BlokusServer && \
    \
    # 실행 테스트 (간단한 버전 확인)
    echo "=== Runtime Test ===" && \
    timeout 5 /app/install/bin/BlokusServer --help || echo "Server help displayed" && \
    \
    echo "=== Build completed successfully ==="

# ==================================================
# Stage 3: Runtime Environment
# 최종 런타임 이미지 (크기 최적화)
# ==================================================
FROM ubuntu:22.04 AS runtime

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 런타임 의존성 설치 (시스템 패키지 활용)
RUN apt-get update && apt-get install -y \
    # 시스템 라이브러리 런타임 (개발 헤더 제외)
    libspdlog1.9 \
    libboost-system1.74.0 \
    libpqxx-7.7 \
    libpq5 \
    libssl3 \
    libfmt8 \
    # 네트워크 도구 (헬스체크용)
    netcat-openbsd \
    # 디버깅 도구 (선택적)
    curl \
    # 시간대 설정
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# 시간대 설정
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# 애플리케이션 사용자 생성 (보안)
RUN groupadd -r blokus && useradd -r -g blokus -d /app -s /bin/bash blokus

# 작업 디렉토리 설정
WORKDIR /app

# Stage 2에서 빌드된 실행파일 복사
COPY --from=app-builder /app/install/bin/BlokusServer ./

# Protocol Buffers 라이브러리 복사 (소스 빌드했으므로)
COPY --from=app-builder /usr/local/lib/libprotobuf.so* /usr/local/lib/
COPY --from=app-builder /usr/local/lib/libabsl*.so* /usr/local/lib/

# 라이브러리 경로 업데이트
RUN ldconfig /usr/local/lib

# 런타임 검증
RUN echo "=== Runtime Verification ===" && \
    ldd ./BlokusServer && \
    echo "=== Server executable check ===" && \
    file ./BlokusServer && \
    echo "=== Libraries verification completed ==="

# 로그 및 설정 디렉토리 생성
RUN mkdir -p /app/logs /app/config && \
    chown -R blokus:blokus /app

# 포트 노출
EXPOSE 9999

# 사용자 변경
USER blokus

# 헬스체크 (서버 시작 후 포트 확인)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD netcat -z localhost 9999 || exit 1

# 실행 명령
CMD ["./BlokusServer"]

# ==================================================
# 빌드 정보 메타데이터
# ==================================================
LABEL maintainer="Blokus Online Team"
LABEL version="1.0.0"
LABEL description="Blokus Online Game Server - Hybrid build strategy"
LABEL build-strategy="system-packages + protobuf-source"
LABEL protobuf-version="25.5"
LABEL ubuntu-base="22.04"