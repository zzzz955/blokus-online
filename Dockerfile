FROM ubuntu:22.04 AS builder

ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

# 기본 도구 및 CMake 설치
RUN apt-get update && apt-get install -y \
    build-essential \
    git \
    curl \
    zip unzip \
    ca-certificates \
    software-properties-common \
    pkg-config \
    tzdata \
    wget \
    bison \
    flex && \
    rm -rf /var/lib/apt/lists/*


# 최신 CMake 설치
RUN wget -O - https://apt.kitware.com/keys/kitware-archive-latest.asc | gpg --dearmor - | \
    tee /etc/apt/trusted.gpg.d/kitware.gpg >/dev/null && \
    echo "deb https://apt.kitware.com/ubuntu/ jammy main" > /etc/apt/sources.list.d/kitware.list && \
    apt-get update && apt-get install -y cmake && rm -rf /var/lib/apt/lists/*

# vcpkg 설치
RUN git clone https://github.com/microsoft/vcpkg.git /opt/vcpkg && \
    /opt/vcpkg/bootstrap-vcpkg.sh

# 서버에 필요한 라이브러리만 설치
RUN /opt/vcpkg/vcpkg install \
    protobuf \
    spdlog \
    nlohmann-json \
    libpqxx \
    openssl \
    boost-system

ENV VCPKG_ROOT=/opt/vcpkg
ENV PATH="${VCPKG_ROOT}:${PATH}"

# 프로젝트 복사
WORKDIR /app
COPY CMakeLists.txt ./
COPY proto/ ./proto/
COPY common/ ./common/
COPY server/ ./server/

# CMake 빌드
RUN mkdir -p build && cd build && \
    cmake .. \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_CXX_STANDARD=17 \
        -DCMAKE_TOOLCHAIN_FILE=/opt/vcpkg/scripts/buildsystems/vcpkg.cmake && \
    make -j$(nproc) BlokusServer

# ========== Runtime ==========
FROM ubuntu:22.04 AS runtime

ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Asia/Seoul

RUN apt-get update && apt-get install -y \
    libpq5 \
    libssl3 \
    libstdc++6 \
    netcat \
    curl && \
    rm -rf /var/lib/apt/lists/*

RUN groupadd -r blokus && useradd -r -g blokus blokus
RUN mkdir -p /app/logs && chown -R blokus:blokus /app

COPY --from=builder /app/build/server/BlokusServer /app/
COPY --from=builder /app/build/proto/ /app/proto/

RUN chmod +x /app/BlokusServer && chown blokus:blokus /app/BlokusServer

USER blokus
WORKDIR /app

EXPOSE 9999

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD netcat -z localhost ${SERVER_PORT:-9999} || exit 1

ENV SERVER_PORT=9999 \
    DB_HOST=postgres \
    DB_PORT=5432 \
    LOG_LEVEL=info \
    LOG_DIRECTORY=/app/logs

CMD ["./BlokusServer"]
