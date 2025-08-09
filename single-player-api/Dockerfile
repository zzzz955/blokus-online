# Node.js 18 Alpine 이미지 사용 (경량화)
FROM node:18-alpine

# 메타데이터 설정
LABEL maintainer="Blokus Team"
LABEL description="Blokus Single Player API Server"
LABEL version="1.0.0"

# 보안을 위한 non-root 사용자 생성
RUN addgroup -g 1001 -S nodejs
RUN adduser -S nextjs -u 1001

# 작업 디렉터리 설정
WORKDIR /app

# 시스템 패키지 업데이트 및 필요한 도구 설치
RUN apk update && apk add --no-cache \
    dumb-init \
    && rm -rf /var/cache/apk/*

# package.json과 package-lock.json 복사 (Docker 캐싱 최적화)
COPY package*.json ./

# 의존성 설치 (프로덕션 전용)
RUN npm ci --only=production && npm cache clean --force

# 소스 코드 복사
COPY . .

# logs 디렉터리 생성
RUN mkdir -p logs

# 파일 소유권 변경
RUN chown -R nextjs:nodejs /app
USER nextjs

# 환경 변수 설정
ENV NODE_ENV=production
ENV PORT=8080

# Health check 추가
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD node -e "require('http').get('http://localhost:8080/api/health/live', (res) => process.exit(res.statusCode === 200 ? 0 : 1))"

# 포트 노출
EXPOSE 8080

# 프로세스 관리자로 dumb-init 사용 (좀비 프로세스 방지)
ENTRYPOINT ["dumb-init", "--"]

# 서버 시작
CMD ["node", "server.js"]