#!/bin/sh
set -e

echo "🚀 Starting Blokus Web application..."

# 데이터베이스 URL이 설정되어 있는지 확인
if [ -z "$DATABASE_URL" ]; then
    echo "❌ DATABASE_URL environment variable is not set"
    exit 1
fi

echo "📊 Running Prisma database migration..."
npx prisma db push --accept-data-loss

echo "✅ Database migration completed"
echo "🌐 Starting Next.js server..."

# Next.js 서버 시작
exec node server.js