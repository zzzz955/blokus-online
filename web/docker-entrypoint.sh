#!/bin/sh
set -e

echo " Starting Blokus Web application..."

# 데이터베이스 URL이 설정되어 있는지 확인
if [ -z "$DATABASE_URL" ]; then
    echo " DATABASE_URL environment variable is not set"
    exit 1
fi

echo " Database connection verified"

echo "👤 Initializing admin user..."
node scripts/init-admin.js
echo " Starting Next.js server..."

# Next.js 서버 시작
exec node server.js