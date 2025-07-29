# ==================================================
# Multi-stage Dockerfile for Blokus Server (Qt 제외)
# ==================================================

# ========== Build Stage ==========
FROM ubuntu:22.04 AS builder

# 비대화형 모드 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 시스템 업데이트 및 기본 도구 설치
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    pkg-config \
    ca-certificates \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# C++ 의존성 설치 (서버 전용, Qt 제외)
RUN apt-get update && apt-get install -y \
    # PostgreSQL 클라이언트 라이브러리
    libpqxx-dev \
    libpq-dev \
    # OpenSSL
    libssl-dev \
    # Boost (system만 필요)
    libboost-system-dev \
    libboost-dev \
    # Protocol Buffers
    libprotobuf-dev \
    protobuf-compiler \
    # JSON 라이브러리
    nlohmann-json3-dev \
    # spdlog (로깅)
    libspdlog-dev \
    && rm -rf /var/lib/apt/lists/*

# 작업 디렉토리 설정
WORKDIR /app

# 소스 코드 복사 (캐시 최적화를 위해 순서 조정)
COPY CMakeLists.txt ./
COPY proto/ ./proto/
COPY common/ ./common/
COPY server/ ./server/

# CMake 빌드 (서버만)
RUN mkdir -p build && cd build && \
    cmake .. \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=17 \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DBUILD_CLIENT=OFF \
    && make -j$(nproc) BlokusServer

# ========== Runtime Stage ==========
FROM ubuntu:22.04 AS runtime

# 비대화형 모드 설정
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 런타임 의존성만 설치
RUN apt-get update && apt-get install -y \
    # PostgreSQL 클라이언트 런타임
    libpqxx-6.4 \
    libpq5 \
    # OpenSSL
    libssl3 \
    # Boost
    libboost-system1.74.0 \
    # Protocol Buffers
    libprotobuf23 \
    # spdlog
    libspdlog1 \
    # 시스템 유틸리티
    curl \
    netcat \
    && rm -rf /var/lib/apt/lists/*

# 애플리케이션 사용자 생성
RUN groupadd -r blokus && useradd -r -g blokus blokus

# 작업 디렉토리 생성
RUN mkdir -p /app/logs && chown -R blokus:blokus /app

# 빌드된 바이너리 복사
COPY --from=builder /app/build/server/BlokusServer /app/
COPY --from=builder /app/build/proto/ /app/proto/

# 권한 설정
RUN chmod +x /app/BlokusServer && chown blokus:blokus /app/BlokusServer

# 사용자 전환
USER blokus

# 작업 디렉토리
WORKDIR /app

# 포트 노출 (환경변수로 설정 가능)
EXPOSE 9999

# 헬스체크 (서버 응답 확인)
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD netcat -z localhost ${SERVER_PORT:-9999} || exit 1

# 환경변수 기본값 설정
ENV SERVER_PORT=9999 \
    DB_HOST=postgres \
    DB_PORT=5432 \
    LOG_LEVEL=info \
    LOG_DIRECTORY=/app/logs

# 서버 실행
CMD ["./BlokusServer"]