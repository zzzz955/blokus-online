# ==================================================
# Multi-Stage Dockerfile - Blokus Online Web
# Next.js 14 기반 웹사이트 배포 최적화
# ==================================================

# ==================================================
# Stage 1: Dependencies Builder
# Node.js 환경에서 의존성 설치
# ==================================================
FROM node:20-alpine AS deps-builder

# 환경 변수 설정
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# 작업 디렉토리 설정
WORKDIR /app

# 시스템 패키지 업데이트 및 필수 도구 설치
RUN apk add --no-cache \
    libc6-compat \
    openssl \
    ca-certificates

# package.json과 package-lock.json 복사
COPY package*.json ./

# 의존성 설치 (production only)
RUN npm ci --only=production --ignore-scripts && \
    npm cache clean --force

# ==================================================
# Stage 2: Application Builder
# Next.js 애플리케이션 빌드
# ==================================================
FROM node:20-alpine AS app-builder

# 환경 변수 설정
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# 작업 디렉토리 설정
WORKDIR /app

# 시스템 패키지 설치
RUN apk add --no-cache \
    libc6-compat \
    openssl \
    ca-certificates

# 전체 프로젝트 소스 복사
COPY package*.json ./
COPY . .

# 개발 의존성 포함하여 설치 (build 시 필요)
RUN npm ci --ignore-scripts

# Prisma 클라이언트 생성
RUN npx prisma generate

# Next.js 빌드 (단계별 실행으로 디버깅 개선)
RUN npm run build
RUN npm prune --production
RUN npm cache clean --force

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

# 런타임에 필요한 패키지들을 사용자 권한으로 설치 (버전 고정)
RUN npm install prisma@5.22.0 bcryptjs@2.4.3

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
LABEL maintainer="Blokus Online Team"
LABEL version="1.0.0"
LABEL description="Blokus Online Web - Next.js 14 Application"
LABEL build-strategy="Multi-stage Docker build"
LABEL node-version="20-alpine"
LABEL framework="Next.js 14"