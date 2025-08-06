# ==================================================
# Stable Multi-Stage Dockerfile - Blokus Online Game Server
# 안정성과 신뢰성 중심의 게임 서버 빌드
# ==================================================

# ==================================================
# Stage 1: Base System with Dependencies
# 시스템 패키지 + 최소한의 vcpkg 조합
# ==================================================
FROM ubuntu:22.04 AS dependencies

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul
ENV VCPKG_ROOT=/opt/vcpkg
ENV VCPKG_DEFAULT_TRIPLET=x64-linux

# 빌드 실패 시 즉시 중단
SHELL ["/bin/bash", "-euxo", "pipefail", "-c"]

# ==================================================
# 시스템 패키지 설치 (검증된 안정 패키지들)
# ==================================================
RUN echo "=== Installing system dependencies ===" && \
    apt-get update && apt-get install -y \
    build-essential \
    ninja-build \
    pkg-config \
    git wget curl ca-certificates tar gzip unzip zip \
    autoconf automake libtool \
    bison flex \
    libpq-dev postgresql-server-dev-all postgresql-client \
    ccache \
    && rm -rf /var/lib/apt/lists/* && \
    echo "=== System packages installed ==="

# ==================================================
# CMake 최신 버전 설치 (3.24+ 필요)
# ==================================================
RUN echo "=== Installing latest CMake ===" && \
    wget -O - https://apt.kitware.com/keys/kitware-archive-latest.asc 2>/dev/null | gpg --dearmor - | tee /usr/share/keyrings/kitware-archive-keyring.gpg >/dev/null && \
    echo 'deb [signed-by=/usr/share/keyrings/kitware-archive-keyring.gpg] https://apt.kitware.com/ubuntu/ jammy main' | tee /etc/apt/sources.list.d/kitware.list >/dev/null && \
    apt-get update && \
    apt-get install -y cmake && \
    cmake --version && \
    echo "=== CMake installation completed ==="

# ==================================================
# vcpkg 설치 (최소한의 패키지만)
# ==================================================
RUN echo "=== Installing vcpkg for minimal packages ===" && \
    git clone https://github.com/Microsoft/vcpkg.git ${VCPKG_ROOT} && \
    cd ${VCPKG_ROOT} && \
    # 안정적인 릴리즈 태그
    git checkout 2024.08.23 && \
    ./bootstrap-vcpkg.sh && \
    echo "=== vcpkg bootstrap completed ==="

# ==================================================
# vcpkg 패키지 설치 (최적화된 단일 설치)
# ==================================================
RUN cd ${VCPKG_ROOT} && \
    echo "=== Installing all required packages via vcpkg ===" && \
    export VCPKG_MAX_CONCURRENCY=$(nproc) && \
    export VCPKG_KEEP_ENV_VARS=VCPKG_MAX_CONCURRENCY && \
    ./vcpkg install spdlog boost-asio boost-system nlohmann-json libpqxx openssl argon2 \
    --triplet=x64-linux \
    --x-buildtrees-root=/tmp/vcpkg-buildtrees \
    --x-install-root=/opt/vcpkg/installed && \
    rm -rf /tmp/vcpkg-buildtrees && \
    ./vcpkg list && \
    echo "=== All vcpkg packages installed successfully ==="

# ==================================================
# Stage 2: Application Builder  
# 프로젝트 빌드 단계
# ==================================================
FROM dependencies AS app-builder

WORKDIR /app

# 캐싱 최적화를 위해 빌드 설정 파일들 먼저 복사
COPY CMakeLists.txt ./
COPY vcpkg.json ./

# 소스코드 복사 (캐시 계층 최적화)
COPY common/ ./common/
COPY server/ ./server/

# ==================================================
# 프로젝트 빌드 (최적화된 접근법)
# ==================================================
RUN echo "=== Building Blokus Game Server ===" && \
    # CMake 구성 (Pure vcpkg)
    cmake -S . -B build \
    -GNinja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_CXX_STANDARD=20 \
    -DCMAKE_TOOLCHAIN_FILE=${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake \
    -DVCPKG_TARGET_TRIPLET=x64-linux \
    -DCMAKE_CXX_FLAGS="-O2 -DNDEBUG" && \
    # 빌드 실행 (최적화된 병렬 빌드)
    ninja -C build -j$(nproc) && \
    # 빌드 결과 검증
    echo "=== Verifying build results ===" && \
    ls -la build/server/ && \
    ldd build/server/BlokusServer | head -10 && \
    # 실행 파일 검증 및 복사
    test -f build/server/BlokusServer && \
    mkdir -p /app/install/bin && \
    cp build/server/BlokusServer /app/install/bin/ && \
    chmod +x /app/install/bin/BlokusServer && \
    echo "=== Build completed successfully ==="

# ==================================================
# Stage 3: Runtime Environment (Game Server Target)
# 최종 런타임 이미지
# ==================================================
FROM ubuntu:22.04 AS game-server

# 환경 변수
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

RUN apt-get update && apt-get install -y \
    libssl3 \
    libpq5 \
    zlib1g \
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    netcat-openbsd \
    curl \
    tzdata \
    jq \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean \
    && echo "Runtime dependencies installed successfully"

# 시간대 설정
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# 애플리케이션 사용자 생성
RUN groupadd -r blokus && useradd -r -g blokus -d /app -s /bin/bash blokus

WORKDIR /app

# 빌드된 실행파일 복사 및 권한 설정
COPY --from=app-builder /app/install/bin/BlokusServer ./
RUN chmod +x ./BlokusServer

# vcpkg 라이브러리 복사 (동적 링크된 것들)
RUN --mount=from=app-builder,source=/opt/vcpkg/installed/x64-linux/lib,target=/tmp/vcpkg-libs,type=bind \
    mkdir -p /usr/local/lib && \
    ls -la /tmp/vcpkg-libs/ && \
    cp /tmp/vcpkg-libs/*.so* /usr/local/lib/ 2>/dev/null || echo "All libraries statically linked" && \
    echo "vcpkg libraries copied (if any dynamic libs exist)"

# 라이브러리 경로 업데이트
RUN ldconfig /usr/local/lib

# 디렉토리 생성 및 권한 설정
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
LABEL version="2.0.0"
LABEL description="Blokus Online Game Server - Stable Hybrid Build"
LABEL build-strategy="System packages + minimal vcpkg"
LABEL base-image="ubuntu:22.04"