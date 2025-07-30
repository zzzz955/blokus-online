# ==================================================
# Multi-Stage Dockerfile - Blokus Online Server
# vcpkg 기반 안정적인 빌드 (CONFIG REQUIRED 완전 해결)
# ==================================================

# ==================================================
# Stage 1: vcpkg Builder
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
# 시스템 패키지 설치
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
    # 추가 유틸리티
    ccache \
    && rm -rf /var/lib/apt/lists/*

# ==================================================
# vcpkg 설치 및 부트스트랩
# ==================================================
RUN echo "=== Installing vcpkg ===" && \
    git clone https://github.com/Microsoft/vcpkg.git ${VCPKG_ROOT} && \
    cd ${VCPKG_ROOT} && \
    # 안정적인 릴리즈 태그로 체크아웃 (옵션)
    git checkout 2024.08.23 && \
    # vcpkg 부트스트랩
    ./bootstrap-vcpkg.sh && \
    # vcpkg 실행 가능성 확인
    ./vcpkg version && \
    echo "=== vcpkg installation completed ==="


# ==================================================
# vcpkg 의존성 설치
# ==================================================
RUN cd ${VCPKG_ROOT} && \
    ./vcpkg install protobuf spdlog boost-asio boost-system nlohmann-json libpqxx openssl \
        --triplet=${VCPKG_DEFAULT_TRIPLET}

# vcpkg 설치 완료

# ==================================================
# Stage 2: Application Builder
# 프로젝트 빌드 단계
# ==================================================
FROM vcpkg-builder AS app-builder

# 작업 디렉토리 설정
WORKDIR /app

# 프로젝트 소스 복사 (proto, common, server만)
COPY proto/ ./proto/
COPY common/ ./common/
COPY server/ ./server/
COPY CMakeLists.txt ./

# vcpkg 설치 확인
RUN echo "=== Checking vcpkg installations ===" && \
    ls -la ${VCPKG_ROOT}/installed/x64-linux/share/ | grep -E "(libpq|protobuf|openssl)" && \
    echo "=== Available packages ===" && \
    ${VCPKG_ROOT}/vcpkg list

# ==================================================
# 프로젝트 빌드 (vcpkg toolchain 사용)
# ==================================================
RUN CMAKE_PATH=$(find ${VCPKG_ROOT}/downloads/tools -name cmake -type f | head -1) && \
    export PATH="$(dirname $CMAKE_PATH):$PATH" && \
    cmake -S . -B build \
        -GNinja \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=20 \
        -DCMAKE_TOOLCHAIN_FILE=${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake \
        -DVCPKG_TARGET_TRIPLET=${VCPKG_DEFAULT_TRIPLET} && \
    ninja -C build -j1 -v && \
    mkdir -p /app/install/bin && \
    cp build/server/BlokusServer /app/install/bin/

# ==================================================
# Stage 3: Runtime Environment
# 최종 런타임 이미지 (크기 최적화)
# ==================================================
FROM ubuntu:22.04 AS runtime

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
LABEL ubuntu-base="22.04"