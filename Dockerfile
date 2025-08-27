# ==================================================
# Multi-Stage Dockerfile - Blokus Online Server
# vcpkg 기반 안정적인 빌드 (CONFIG REQUIRED 완전 해결)
# ==================================================

# ==================================================
# Stage 1: vcpkg Builder (캐시 최적화)
# Microsoft vcpkg를 사용한 안정적인 의존성 관리
# ==================================================
FROM ubuntu:22.04 AS vcpkg-builder

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul
ENV VCPKG_ROOT=/opt/vcpkg
ENV VCPKG_DEFAULT_TRIPLET=x64-linux
ENV VCPKG_FEATURE_FLAGS=manifests,versions,binarycaching

# 빌드 실패 시 즉시 중단
SHELL ["/bin/bash", "-euxo", "pipefail", "-c"]

# ==================================================
# 시스템 패키지 설치 (캐시 가능한 레이어)
# ==================================================
RUN echo "=== Installing build dependencies ===" && \
    apt-get update && apt-get install -y \
    # 빌드 도구
    build-essential \
    ninja-build \
    pkg-config \
    # 버전 관리 및 다운로드
    git \
    wget \
    curl \
    ca-certificates \
    # 압축 도구
    tar \
    gzip \
    unzip \
    zip \
    # vcpkg 의존성
    autoconf \
    automake \
    libtool \
    bison \
    flex \
    # PostgreSQL 빌드 의존성 (libpq 빌드용)
    libpq-dev \
    postgresql-server-dev-all \
    # 추가 유틸리티 (컴파일 캐시)
    ccache \
    gnupg \
    && rm -rf /var/lib/apt/lists/*

# Kitware APT 추가 + CMake 설치 블록 보강
RUN mkdir -p /etc/apt/keyrings \
    && . /etc/os-release \
    && wget -O - https://apt.kitware.com/keys/kitware-archive-latest.asc \
    | gpg --dearmor -o /etc/apt/keyrings/kitware.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/kitware.gpg] https://apt.kitware.com/ubuntu/ ${UBUNTU_CODENAME} main" \
    > /etc/apt/sources.list.d/kitware.list \
    && apt-get update && apt-get install -y cmake \
    && cmake --version

# ==================================================
# vcpkg 설치 및 부트스트랩 (캐시 가능한 레이어)
# ==================================================
RUN echo "=== Installing vcpkg ===" && \
    git clone https://github.com/Microsoft/vcpkg.git ${VCPKG_ROOT} && \
    cd ${VCPKG_ROOT} && \
    # 안정적인 릴리즈 태그로 체크아웃 (2024.09.23)
    git checkout 2024.09.23 && \
    # vcpkg 부트스트랩
    ./bootstrap-vcpkg.sh && \
    # vcpkg 실행 가능성 확인
    ./vcpkg version && \
    echo "=== vcpkg installation completed ==="

# ==================================================
# 서버 전용 의존성 매니페스트 파일 복사 (Qt 제외로 libsystemd 에러 해결)
# 의존성이 변경되지 않으면 이후 레이어들이 모두 캐시됨
# ==================================================
COPY vcpkg-server.json ${VCPKG_ROOT}/vcpkg.json

# ==================================================
# vcpkg 의존성 설치 (병렬 빌드 + 캐시 최적화)
# ==================================================
RUN cd ${VCPKG_ROOT} \
    && export VCPKG_MAX_CONCURRENCY=$(nproc) \
    && export VCPKG_KEEP_ENV_VARS=VCPKG_MAX_CONCURRENCY \
    && export PATH="/usr/lib/ccache:$PATH" \
    && export CCACHE_DIR=/tmp/ccache \
    && export VCPKG_FEATURE_FLAGS=manifests,versions,binarycaching,compilertracking \
    && echo "=== Starting vcpkg dependency installation ===" \
    && ./vcpkg install --triplet=${VCPKG_DEFAULT_TRIPLET} \
    --x-buildtrees-root=/tmp/vcpkg-buildtrees \
    --x-install-root=/opt/vcpkg/installed \
    --x-packages-root=/tmp/vcpkg-packages \
    --clean-after-build \
    && echo "=== Verifying jwt-cpp installation ===" \
    && ./vcpkg list | sed -n '/jwt/p' \
    && ( \
    test -f "/opt/vcpkg/installed/${VCPKG_DEFAULT_TRIPLET}/share/jwt-cpp/jwt-cpp-config.cmake" \
    || test -f "/opt/vcpkg/installed/${VCPKG_DEFAULT_TRIPLET}/share/jwt-cpp/jwt-cppConfig.cmake" \
    || test -f "/opt/vcpkg/installed/${VCPKG_DEFAULT_TRIPLET}/lib/cmake/jwt-cpp/jwt-cpp-config.cmake" \
    || (echo "jwt-cpp CMake config not found; dumping tree:" \
    && find "/opt/vcpkg/installed/${VCPKG_DEFAULT_TRIPLET}" -maxdepth 4 -type f -iname "*jwt*config*.cmake" -print \
    && exit 1) \
    ) \
    && rm -rf /tmp/vcpkg-buildtrees /tmp/vcpkg-packages /tmp/ccache \
    && ./vcpkg list \
    && echo "=== Server-only vcpkg dependencies installation completed ==="

# vcpkg 의존성 설치 완료 (소스코드와 독립적으로 캐시됨)

# ==================================================
# Stage 2: Application Builder (캐시 최적화)
# 프로젝트 빌드 단계
# ==================================================
FROM vcpkg-builder AS app-builder

# 작업 디렉토리 설정
WORKDIR /app

# 루트 전체 복사 (.dockerignore로 제외 관리)
COPY . .

# vcpkg 설치 확인 (디버깅용)
RUN echo "=== Checking vcpkg installations ===" && \
    ls -la ${VCPKG_ROOT}/installed/x64-linux/share/ | grep -E "(libpq|openssl|spdlog|jwt-cpp)" && \
    echo "=== Available packages ===" && \
    ${VCPKG_ROOT}/vcpkg list && \
    echo "=== Checking jwt-cpp specifically ===" && \
    find ${VCPKG_ROOT}/installed/x64-linux -name "*jwt*" -type d && \
    find ${VCPKG_ROOT}/installed/x64-linux -name "*jwt*" -type f

# ==================================================
# 프로젝트 빌드 (vcpkg toolchain + ccache 사용)
# ==================================================
# ccache 설정으로 컴파일 시간 단축
RUN export PATH="/usr/lib/ccache:$PATH" && \
    export CCACHE_DIR=/tmp/ccache && \
    export CMAKE_C_COMPILER_LAUNCHER=ccache && \
    export CMAKE_CXX_COMPILER_LAUNCHER=ccache && \
    # CMake 구성 전 디버깅
    echo "=== CMake Configuration Debug ===" && \
    echo "VCPKG_ROOT: ${VCPKG_ROOT}" && \
    echo "CMAKE_TOOLCHAIN_FILE: ${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake" && \
    echo "VCPKG_TARGET_TRIPLET: ${VCPKG_DEFAULT_TRIPLET}" && \
    ls -la ${VCPKG_ROOT}/installed/${VCPKG_DEFAULT_TRIPLET}/share/jwt-cpp/ 2>/dev/null || echo "jwt-cpp share directory not found" && \
    echo "=== Searching for jwt-cpp CMake config files ===" && \
    find ${VCPKG_ROOT}/installed/${VCPKG_DEFAULT_TRIPLET} -maxdepth 4 -type f -iname "*jwt*config*.cmake" -print || true && \
    # CMake 구성 with cache cleanup and robust error handling
    echo "=== Using CMake ===" && cmake --version \
    && echo "=== Toolchain === ${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake" \
    && rm -rf build \
    && cmake -S . -B build \
          -GNinja \
          -DCMAKE_BUILD_TYPE=Release \
          -DCMAKE_CXX_STANDARD=20 \
          -DCMAKE_TOOLCHAIN_FILE=${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake \
          -DVCPKG_TARGET_TRIPLET=${VCPKG_DEFAULT_TRIPLET} \
          -DCMAKE_C_COMPILER_LAUNCHER=ccache \
          -DCMAKE_CXX_COMPILER_LAUNCHER=ccache \
          -DCMAKE_FIND_DEBUG_MODE=ON \
     || (echo "=== CMake Configuration Failed ==="; \
         echo "Build directory contents:"; ls -la build/ 2>/dev/null || echo "No build directory"; \
         echo "CMake Files:"; find build/ -name "*.log" 2>/dev/null || echo "No log files found"; \
         if [ -f "build/CMakeFiles/CMakeError.log" ]; then echo "=== CMakeError.log ==="; cat build/CMakeFiles/CMakeError.log; fi; \
         if [ -f "build/CMakeFiles/CMakeOutput.log" ]; then echo "=== CMakeOutput.log ==="; cat build/CMakeFiles/CMakeOutput.log; fi; \
         exit 1) \
    && \
    # 병렬 빌드 (CPU 코어 수 활용)
    ninja -C build -j$(nproc) -v && \
    # 빌드 결과 설치 디렉토리에 복사
    mkdir -p /app/install/bin && \
    cp build/server/BlokusServer /app/install/bin/ && \
    # 빌드 캐시 정리
    rm -rf /tmp/ccache && \
    echo "=== Application build completed ==="

# ==================================================
# Stage 3: Runtime Environment (Game Server Target)
# 최종 런타임 이미지 (크기 최적화)
# ==================================================
FROM ubuntu:22.04 AS game-server

# 환경 변수 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 런타임 의존성 설치 (최소한)
RUN apt-get update && apt-get install -y \
    # 핵심 시스템 라이브러리
    libssl3 \
    libpq5 \
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

# 시간대 설정
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# 애플리케이션 사용자 생성 (보안)
RUN groupadd -r blokus && useradd -r -g blokus -d /app -s /bin/bash blokus

# 작업 디렉토리 설정
WORKDIR /app

# Stage 2에서 빌드된 실행파일 복사
COPY --from=app-builder /app/install/bin/BlokusServer ./

# vcpkg에서 빌드된 라이브러리들 복사 (필요한 것만)
RUN mkdir -p /usr/local/lib
COPY --from=app-builder /opt/vcpkg/installed/x64-linux/lib/ /usr/local/lib/

# 라이브러리 경로 업데이트
RUN ldconfig /usr/local/lib

# 런타임 준비 완료

# 로그 및 설정 디렉토리 생성
RUN mkdir -p /app/logs /app/config && \
    chown -R blokus:blokus /app

# 포트 노출
EXPOSE 9999

# 사용자 변경
USER blokus

# 헬스체크 임시 비활성화 (디버깅용)
# HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=5 \
#     CMD netcat -z localhost 9999 || exit 1

# 실행 명령
CMD ["./BlokusServer"]

# ==================================================
# 빌드 정보 메타데이터
# ==================================================
LABEL maintainer="Blokus Online Team"
LABEL version="1.0.0"
LABEL description="Blokus Online Game Server - vcpkg stable build"
LABEL build-strategy="Microsoft vcpkg"
LABEL vcpkg-triplet="x64-linux"