# ==================================================
# Multi-Stage Dockerfile - Blokus Online Server
# 안정성 최우선, CONFIG REQUIRED 문제 해결
# ==================================================

# ==================================================
# Stage 1: Dependencies Builder
# 모든 라이브러리를 소스에서 빌드하여 CONFIG 지원 보장
# ==================================================
FROM ubuntu:22.04 AS dependencies-builder

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul
ENV CMAKE_BUILD_TYPE=Release
ENV PARALLEL_JOBS=4

# 시스템 패키지 업데이트 및 기본 도구 설치
RUN apt-get update && apt-get install -y \
    # 빌드 도구
    build-essential \
    cmake \
    ninja-build \
    pkg-config \
    ccache \
    # 네트워크 및 다운로드 도구
    wget \
    curl \
    git \
    ca-certificates \
    # 압축 도구
    tar \
    gzip \
    unzip \
    # PostgreSQL 개발 헤더
    libpq-dev \
    postgresql-server-dev-all \
    # OpenSSL 개발 헤더 (시스템 패키지로 충분)
    libssl-dev \
    # 기타 필수 라이브러리
    zlib1g-dev \
    libbz2-dev \
    && rm -rf /var/lib/apt/lists/*

# ccache 설정
RUN ccache --set-config=max_size=2G && \
    ccache --set-config=compression=true

# 작업 디렉토리 설정
WORKDIR /tmp/deps

# ==================================================
# Protocol Buffers 빌드 (CONFIG 지원)
# ==================================================
ARG PROTOBUF_VERSION=25.5
RUN echo "=== Building Protocol Buffers ${PROTOBUF_VERSION} ===" && \
    wget -q https://github.com/protocolbuffers/protobuf/archive/v${PROTOBUF_VERSION}.tar.gz -O protobuf.tar.gz && \
    tar -xzf protobuf.tar.gz && \
    cd protobuf-${PROTOBUF_VERSION} && \
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -Dprotobuf_BUILD_TESTS=OFF \
        -Dprotobuf_BUILD_EXAMPLES=OFF \
        -DCMAKE_CXX_STANDARD=20 && \
    ninja -C build -j${PARALLEL_JOBS} && \
    ninja -C build install && \
    ldconfig && \
    cd .. && rm -rf protobuf-${PROTOBUF_VERSION} protobuf.tar.gz

# ==================================================
# spdlog 빌드 (CONFIG 지원)
# ==================================================
ARG SPDLOG_VERSION=1.15.3
RUN echo "=== Building spdlog ${SPDLOG_VERSION} ===" && \
    wget -q https://github.com/gabime/spdlog/archive/v${SPDLOG_VERSION}.tar.gz -O spdlog.tar.gz && \
    tar -xzf spdlog.tar.gz && \
    cd spdlog-${SPDLOG_VERSION} && \
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DSPDLOG_BUILD_TESTS=OFF \
        -DSPDLOG_BUILD_EXAMPLE=OFF \
        -DCMAKE_CXX_STANDARD=20 && \
    ninja -C build -j${PARALLEL_JOBS} && \
    ninja -C build install && \
    cd .. && rm -rf spdlog-${SPDLOG_VERSION} spdlog.tar.gz

# ==================================================
# nlohmann-json 설치 (헤더 온리, CONFIG 지원)
# ==================================================
ARG JSON_VERSION=3.12.0
RUN echo "=== Installing nlohmann-json ${JSON_VERSION} ===" && \
    wget -q https://github.com/nlohmann/json/archive/v${JSON_VERSION}.tar.gz -O nlohmann-json.tar.gz && \
    tar -xzf nlohmann-json.tar.gz && \
    cd json-${JSON_VERSION} && \
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DJSON_BuildTests=OFF \
        -DJSON_Install=ON && \
    ninja -C build -j${PARALLEL_JOBS} && \
    ninja -C build install && \
    cd .. && rm -rf json-${JSON_VERSION} nlohmann-json.tar.gz

# ==================================================
# Boost 빌드 (system component만, CONFIG 지원)
# ==================================================
ARG BOOST_VERSION=1.84.0
ARG BOOST_VERSION_UNDERSCORE=1_84_0
RUN echo "=== Building Boost ${BOOST_VERSION} ===" && \
    wget -q https://boostorg.jfrog.io/artifactory/main/release/${BOOST_VERSION}/source/boost_${BOOST_VERSION_UNDERSCORE}.tar.bz2 -O boost.tar.bz2 && \
    tar -xjf boost.tar.bz2 && \
    cd boost_${BOOST_VERSION_UNDERSCORE} && \
    ./bootstrap.sh --prefix=/usr/local --with-libraries=system && \
    ./b2 -j${PARALLEL_JOBS} \
        variant=release \
        link=shared \
        threading=multi \
        runtime-link=shared \
        cxxstd=20 \
        install && \
    cd .. && rm -rf boost_${BOOST_VERSION_UNDERSCORE} boost.tar.bz2

# ==================================================
# libpqxx 빌드 (CONFIG 지원)
# ==================================================
ARG LIBPQXX_VERSION=7.9.2
RUN echo "=== Building libpqxx ${LIBPQXX_VERSION} ===" && \
    wget -q https://github.com/jtv/libpqxx/archive/${LIBPQXX_VERSION}.tar.gz -O libpqxx.tar.gz && \
    tar -xzf libpqxx.tar.gz && \
    cd libpqxx-${LIBPQXX_VERSION} && \
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DSKIP_BUILD_TEST=ON \
        -DCMAKE_CXX_STANDARD=20 && \
    ninja -C build -j${PARALLEL_JOBS} && \
    ninja -C build install && \
    ldconfig && \
    cd .. && rm -rf libpqxx-${LIBPQXX_VERSION} libpqxx.tar.gz

# ==================================================
# CMake 라이브러리 검증
# ==================================================
RUN echo "=== Verifying installed libraries ===" && \
    pkg-config --exists protobuf && echo "✓ protobuf found" && \
    find /usr/local -name "*Config.cmake" -o -name "*-config.cmake" | grep -E "(protobuf|spdlog|nlohmann|pqxx)" | head -20 && \
    ldconfig -p | grep -E "(protobuf|spdlog|pqxx|boost)" && \
    echo "=== Dependencies verification completed ==="

# ==================================================
# Stage 2: Application Builder  
# 프로젝트 빌드 단계
# ==================================================
FROM dependencies-builder AS app-builder

# 작업 디렉토리 설정
WORKDIR /app

# CMake 최소 버전 확인
RUN cmake --version

# 프로젝트 소스 복사 (proto, common, server만)
COPY proto/ ./proto/
COPY common/ ./common/
COPY server/ ./server/
COPY CMakeLists.txt ./

# ==================================================
# 프로젝트 빌드
# ==================================================
RUN echo "=== Building Blokus Server ===" && \
    # CMake 설정
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=20 \
        -DCMAKE_INSTALL_PREFIX=/app/install \
        -DCMAKE_PREFIX_PATH="/usr/local" \
        -DBoost_USE_STATIC_LIBS=OFF \
        -DBoost_USE_MULTITHREADED=ON \
        -DCMAKE_VERBOSE_MAKEFILE=ON && \
    \
    # 의존성 확인
    echo "=== CMake Configuration Summary ===" && \
    cat build/CMakeCache.txt | grep -E "(protobuf|spdlog|nlohmann|Boost|pqxx|OpenSSL)" | head -20 && \
    \
    # 빌드 실행
    ninja -C build -j${PARALLEL_JOBS} -v && \
    \
    # 설치
    ninja -C build install && \
    \
    # 빌드 결과 검증
    echo "=== Build Verification ===" && \
    ls -la /app/install/bin/ && \
    ldd /app/install/bin/BlokusServer && \
    echo "=== Build completed successfully ==="

# ==================================================
# Stage 3: Runtime Environment
# 최종 런타임 이미지 (크기 최적화)
# ==================================================
FROM ubuntu:22.04 AS runtime

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 런타임 패키지만 설치
RUN apt-get update && apt-get install -y \
    # 런타임 라이브러리
    libssl3 \
    libpq5 \
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

# Stage 2에서 빌드된 결과물 복사
COPY --from=app-builder /app/install/bin/BlokusServer ./
COPY --from=app-builder /usr/local/lib/libprotobuf.so* /usr/local/lib/
COPY --from=app-builder /usr/local/lib/libspdlog.so* /usr/local/lib/
COPY --from=app-builder /usr/local/lib/libpqxx.so* /usr/local/lib/
COPY --from=app-builder /usr/local/lib/libboost_system.so* /usr/local/lib/

# 라이브러리 경로 업데이트
RUN ldconfig /usr/local/lib

# 로그 디렉토리 생성
RUN mkdir -p /app/logs /app/config && \
    chown -R blokus:blokus /app

# 포트 노출
EXPOSE 9999

# 사용자 변경
USER blokus

# 헬스체크
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD netcat -z localhost 9999 || exit 1

# 실행 명령
CMD ["./BlokusServer"]

# ==================================================
# 빌드 정보 메타데이터
# ==================================================
LABEL maintainer="Blokus Online Team"
LABEL version="1.0.0"
LABEL description="Blokus Online Game Server - Optimized for CI/CD"
LABEL build-stage="production"