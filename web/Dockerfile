# ==================================================
# Multi-Stage Dockerfile - Bloblo Web
# Next.js 14 기반 웹사이트 배포 최적화
# ==================================================

# ==================================================
# Stage 1: Dependencies Builder (캐시 최적화)
# Node.js 환경에서 의존성 설치
# ==================================================
FROM node:20-alpine AS deps-builder

# 환경 변수 설정
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# 작업 디렉토리 설정
WORKDIR /app

# 시스템 패키지 설치 (캐시 가능한 레이어)
RUN apk add --no-cache \
    libc6-compat \
    openssl \
    ca-certificates \
    # Native 모듈 빌드를 위한 도구들
    python3 \
    make \
    g++ \
    gcc \
    musl-dev

# package.json 파일들만 먼저 복사 (캐시 최적화 핵심)
# 의존성이 변경되지 않으면 npm ci 단계가 캐시됨
COPY package*.json ./

# production 의존성 설치 (캐시됨) - 최적화
RUN npm ci --only=production --no-audit --no-fund && \
    npm cache clean --force

# ==================================================
# Stage 2: Application Builder (캐시 최적화)
# Next.js 애플리케이션 빌드
# ==================================================
FROM node:20-alpine AS app-builder

# 환경 변수 설정
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# 작업 디렉토리 설정
WORKDIR /app

# 시스템 패키지 설치 (캐시 가능한 레이어)
RUN apk add --no-cache \
    libc6-compat \
    openssl \
    ca-certificates \
    # Native 모듈 빌드를 위한 도구들
    python3 \
    make \
    g++ \
    gcc \
    musl-dev

# package.json 파일들 먼저 복사 (캐시 최적화)
COPY package*.json ./

# 개발 의존성 포함하여 설치 (빌드 시 필요)
RUN npm ci

# Prisma schema 먼저 복사 (캐시 최적화)
COPY prisma/ ./prisma/

# Prisma 클라이언트 생성 (schema 변경 시에만 재실행)
RUN npx prisma generate

# 나머지 소스코드 복사 (가장 자주 변경되는 부분)
COPY . .

# Next.js 빌드 (최적화된 단계별 실행)
RUN npm run build && \
    npm prune --production && \
    npm cache clean --force

# ==================================================
# Stage 3: Runtime Environment
# 최종 실행 환경 (크기 최적화)
# ==================================================
FROM node:20-alpine AS runtime

# 환경 변수 설정
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000

# 시스템 패키지 설치
RUN apk add --no-cache \
    libc6-compat \
    openssl \
    ca-certificates \
    # 헬스체크용
    curl \
    # Prisma 실행을 위한 OpenSSL
    openssl-dev

# 애플리케이션 사용자 생성 (보안)
RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 nextjs

# 작업 디렉토리 설정
WORKDIR /app

# 필요한 디렉토리 생성
RUN mkdir -p /app/.next/cache && \
    chown -R nextjs:nodejs /app

# Stage 2에서 빌드된 애플리케이션 복사
COPY --from=app-builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=app-builder --chown=nextjs:nodejs /app/.next/static ./.next/static

# Prisma 관련 파일들 복사
COPY --from=app-builder --chown=nextjs:nodejs /app/node_modules/.prisma ./node_modules/.prisma
COPY --from=app-builder --chown=nextjs:nodejs /app/node_modules/prisma ./node_modules/prisma
COPY --from=app-builder --chown=nextjs:nodejs /app/node_modules/@prisma ./node_modules/@prisma
COPY --from=app-builder --chown=nextjs:nodejs /app/node_modules/.bin/prisma ./node_modules/.bin/prisma


# public 폴더는 더 이상 필요하지 않음 (GitHub Releases로 전환)
# COPY --chown=nextjs:nodejs ./public /app/public

# prisma 폴더 복사
COPY --chown=nextjs:nodejs ./prisma /app/prisma

# scripts 폴더 복사
COPY --chown=nextjs:nodejs ./scripts /app/scripts

# package.json에서 실행에 필요한 정보만 복사
COPY --from=app-builder --chown=nextjs:nodejs /app/package.json ./package.json

# 엔트리포인트 스크립트 복사
COPY --chown=nextjs:nodejs ./docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# 사용자 변경
USER nextjs

# Runtime에 필요한 dependencies 복사 (빌드 단계에서 이미 설치됨)
COPY --from=app-builder --chown=nextjs:nodejs /app/node_modules ./node_modules

# 포트 노출
EXPOSE 3000

# 헬스체크
# HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
#     CMD curl -f http://localhost:3000/api/health || exit 1

# 실행 명령
CMD ["/app/docker-entrypoint.sh"]

# ==================================================
# 빌드 정보 메타데이터
# ==================================================
LABEL maintainer="Bloblo Team"
LABEL version="1.0.0"
LABEL description="Bloblo Web - Next.js 14 Application"
LABEL build-strategy="Multi-stage Docker build"
LABEL node-version="20-alpine"
LABEL framework="Next.js 14"