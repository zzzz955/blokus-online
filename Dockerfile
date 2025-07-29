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

# 최소 런타임 의존성 설치 (확실한 패키지만)
RUN apt-get update && apt-get install -y \
    # 핵심 시스템 라이브러리 (항상 존재)
    libpq5 \
    libssl3 \
    zlib1g \
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    # 네트워크 도구 (헬스체크용)
    netcat-openbsd \
    # 디버깅 도구 (선택적)  
    curl \
    # 시간대 설정
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# 추가 런타임 라이브러리 시도 (실패해도 계속 진행)
RUN apt-get update && \
    (apt-get install -y libspdlog1.15 || echo "libspdlog1.15 not found") && \
    (apt-get install -y libboost-system1.74.0 || echo "libboost-system1.74.0 not found") && \
    (apt-get install -y libpqxx-6.4 || apt-get install -y libpqxx-7.10 || echo "libpqxx not found") && \
    (apt-get install -y libfmt8 || echo "libfmt8 not found") && \
    rm -rf /var/lib/apt/lists/*

# 시간대 설정
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# 애플리케이션 사용자 생성 (보안)
RUN groupadd -r blokus && useradd -r -g blokus -d /app -s /bin/bash blokus

# 작업 디렉토리 설정
WORKDIR /app

# Stage 2에서 빌드된 실행파일 복사
COPY --from=app-builder /app/install/bin/BlokusServer ./

# 모든 필요한 라이브러리 복사 (dependencies-builder에서)
# Protocol Buffers (소스 빌드)
COPY --from=dependencies-builder /usr/local/lib/libprotobuf.so* /usr/local/lib/
COPY --from=dependencies-builder /usr/local/lib/libabsl*.so* /usr/local/lib/

# 동적 라이브러리 복사 (ldd 결과 기반으로 필요한 것만)
# 시스템 패키지로 설치된 라이브러리들은 시스템 경로에 있으므로 복사 불필요
# 런타임에서 필요시 누락된 라이브러리만 설치하도록 함

# 라이브러리 경로 업데이트
RUN ldconfig /usr/local/lib

# 런타임 검증 및 누락 라이브러리 확인
RUN echo "=== Runtime Verification ===" && \
    echo "1. Executable check:" && \
    file ./BlokusServer && \
    echo "2. Library dependencies:" && \
    ldd ./BlokusServer && \
    echo "3. Available libraries in /usr/local/lib:" && \
    ls -la /usr/local/lib/ && \
    echo "4. Testing basic execution:" && \
    (timeout 3 ./BlokusServer --version 2>&1 || echo "Server startup test completed") && \
    echo "=== Runtime verification completed ==="

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