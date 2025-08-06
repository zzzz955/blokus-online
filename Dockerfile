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
    # 기본 빌드 도구
    build-essential \
    cmake \
    ninja-build \
    pkg-config \
    git \
    curl \
    wget \
    ca-certificates \
    # C++ 라이브러리들 (vcpkg와 충돌하지 않는 것들만)
    libssl-dev \
    libpq-dev \
    libboost-all-dev \
    # PostgreSQL 빌드를 위한 시스템 의존성
    postgresql-server-dev-all \
    # 압축 및 유틸리티
    tar \
    gzip \
    unzip \
    zip \
    && rm -rf /var/lib/apt/lists/* && \
    echo "=== System packages installed ==="

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
# vcpkg 매니페스트 (CONFIG REQUIRED 패키지들)
# ==================================================
RUN cd ${VCPKG_ROOT} && \
    echo '{ 
      "name": "blokus-game-server",
      "version": "1.0.0", 
      "dependencies": [
        "spdlog",
        "libpqxx",
        "argon2",
        "nlohmann-json"
      ]
    }' > vcpkg.json && \
    echo "=== vcpkg manifest created ==="

# ==================================================
# vcpkg 의존성 설치 (안정적이고 단순한 방식)
# ==================================================
RUN cd ${VCPKG_ROOT} && \
    echo "=== Installing vcpkg packages ===" && \
    # 병렬 빌드 설정 (과도하지 않게)
    export VCPKG_MAX_CONCURRENCY=4 && \
    # 단순한 설치 (시간 제한 없음, 최적화 플래그 최소화)
    ./vcpkg install --triplet=x64-linux --clean-after-build && \
    # 설치 확인
    ./vcpkg list && \
    # 불필요한 빌드 파일 정리
    rm -rf /tmp/vcpkg-* || echo "Cleanup completed" && \
    echo "=== vcpkg packages installed successfully ==="

# ==================================================
# Stage 2: Application Builder  
# 프로젝트 빌드 단계
# ==================================================
FROM dependencies AS app-builder

WORKDIR /app

# CMake 파일 먼저 복사
COPY CMakeLists.txt ./

# 소스코드 복사
COPY common/ ./common/
COPY server/ ./server/

# ==================================================
# 프로젝트 빌드 (하이브리드 접근법)
# ==================================================
RUN echo "=== Building Blokus Game Server ===" && \
    # CMake 구성 (vcpkg toolchain 사용)
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=20 \
        -DCMAKE_TOOLCHAIN_FILE=${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake \
        -DVCPKG_TARGET_TRIPLET=x64-linux && \
    # 빌드 실행 (병렬 빌드 제한)
    ninja -C build -j4 && \
    # 빌드 결과 복사
    mkdir -p /app/install/bin && \
    cp build/server/BlokusServer /app/install/bin/ && \
    echo "=== Build completed successfully ==="

# ==================================================
# Stage 3: Runtime Environment (Game Server Target)
# 최종 런타임 이미지
# ==================================================
FROM ubuntu:22.04 AS game-server

# 환경 변수
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 런타임 의존성 설치 (최소한)
RUN apt-get update && apt-get install -y \
    # 핵심 런타임 라이브러리
    libssl3 \
    libpq5 \
    libboost-system1.74.0 \
    libboost-thread1.74.0 \
    # 시스템 라이브러리
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    # 네트워크 도구
    netcat-openbsd \
    curl \
    # 시간대 설정
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# 시간대 설정
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# 애플리케이션 사용자 생성
RUN groupadd -r blokus && useradd -r -g blokus -d /app -s /bin/bash blokus

WORKDIR /app

# 빌드된 실행파일 복사
COPY --from=app-builder /app/install/bin/BlokusServer ./

# vcpkg 라이브러리 복사 (필요한 것들)
RUN mkdir -p /usr/local/lib
COPY --from=app-builder /opt/vcpkg/installed/x64-linux/lib/libspdlog.so* /usr/local/lib/ || echo "spdlog static linked"
COPY --from=app-builder /opt/vcpkg/installed/x64-linux/lib/libargon2.so* /usr/local/lib/ || echo "argon2 static linked"
COPY --from=app-builder /opt/vcpkg/installed/x64-linux/lib/libpqxx*.so* /usr/local/lib/ || echo "libpqxx static linked"

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