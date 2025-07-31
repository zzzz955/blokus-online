#!/bin/bash

# ==================================================
# PostgreSQL 다중 데이터베이스 초기화 스크립트
# 수동 생성을 위한 가이드 스크립트
# ==================================================

set -e
set -u

echo "=== Blokus Online Database Setup Guide ==="
echo ""
echo "이 스크립트는 참고용입니다. 수동으로 다음 명령어를 실행하세요:"
echo ""
echo "1. PostgreSQL 컨테이너에 접속:"
echo "   docker-compose exec postgres psql -U \$POSTGRES_USER"
echo ""
echo "2. 웹 애플리케이션용 데이터베이스 생성:"
echo "   CREATE DATABASE blokus_web;"
echo "   GRANT ALL PRIVILEGES ON DATABASE blokus_web TO \$POSTGRES_USER;"
echo ""
echo "3. 웹 데이터베이스 스키마 생성:"
echo "   \\c blokus_web"
echo "   \\i /docker-entrypoint-initdb.d/02-create-web-tables.sql"
echo ""
echo "또는 호스트에서 직접 실행:"
echo "   docker-compose exec -T postgres psql -U \$POSTGRES_USER < sql/02-create-web-tables.sql"
echo ""
echo "=== 데이터베이스 구성 ==="
echo "- \$POSTGRES_DB (게임 서버용): 자동 생성됨"
echo "- blokus_web (웹 애플리케이션용): 수동 생성 필요"
echo ""