#!/bin/bash

# Blokus Single Player API 개발 환경 시작 스크립트

set -e

# 색상 설정
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Blokus Single Player API - Development Setup${NC}"
echo -e "${BLUE}==============================================${NC}"

# 환경 파일 확인
if [ ! -f ".env" ]; then
    echo -e "${YELLOW}⚠️  .env file not found. Creating from .env.example...${NC}"
    if [ -f ".env.example" ]; then
        cp .env.example .env
        echo -e "${GREEN}✅ .env file created. Please edit it with your configuration.${NC}"
    else
        echo -e "${RED}❌ .env.example not found. Please create .env file manually.${NC}"
        exit 1
    fi
fi

# Node.js 버전 확인
echo -e "${YELLOW}📋 Checking Node.js version...${NC}"
node_version=$(node --version 2>/dev/null || echo "not installed")
echo -e "   Node.js version: ${node_version}"

if [[ $node_version == "not installed" ]]; then
    echo -e "${RED}❌ Node.js is not installed. Please install Node.js 18 or higher.${NC}"
    exit 1
fi

# npm 패키지 설치
echo -e "${YELLOW}📦 Installing dependencies...${NC}"
if [ ! -d "node_modules" ]; then
    npm install
else
    echo -e "${GREEN}✅ Dependencies already installed${NC}"
fi

# 로그 디렉터리 생성
echo -e "${YELLOW}📁 Creating logs directory...${NC}"
mkdir -p logs

# Docker Compose 서비스 시작 (PostgreSQL, Redis)
echo -e "${YELLOW}🐳 Starting database services...${NC}"
if command -v docker-compose >/dev/null 2>&1; then
    echo -e "   Starting PostgreSQL and Redis..."
    docker-compose up -d postgres redis
    
    # 서비스가 준비될 때까지 대기
    echo -e "${YELLOW}⏳ Waiting for services to be ready...${NC}"
    sleep 5
    
    # PostgreSQL 헬스체크
    echo -e "   Checking PostgreSQL..."
    for i in {1..30}; do
        if docker-compose exec -T postgres pg_isready -U ${DB_USER:-admin} -d blokus_online >/dev/null 2>&1; then
            echo -e "${GREEN}   ✅ PostgreSQL is ready${NC}"
            break
        fi
        if [ $i -eq 30 ]; then
            echo -e "${RED}   ❌ PostgreSQL failed to start${NC}"
            exit 1
        fi
        sleep 1
    done
    
    # Redis 헬스체크
    echo -e "   Checking Redis..."
    for i in {1..10}; do
        if docker-compose exec -T redis redis-cli ping >/dev/null 2>&1; then
            echo -e "${GREEN}   ✅ Redis is ready${NC}"
            break
        fi
        if [ $i -eq 10 ]; then
            echo -e "${RED}   ❌ Redis failed to start${NC}"
            exit 1
        fi
        sleep 1
    done
else
    echo -e "${YELLOW}⚠️  Docker Compose not found. Make sure PostgreSQL and Redis are running manually.${NC}"
fi

# 개발 서버 시작
echo -e "${YELLOW}🌟 Starting development server...${NC}"
echo -e "${GREEN}✅ Setup completed!${NC}"
echo
echo -e "${BLUE}📍 Server will be available at:${NC}"
echo -e "   🌐 API: http://localhost:8080/api"
echo -e "   ❤️  Health: http://localhost:8080/api/health"
echo -e "   📚 Docs: http://localhost:8080/api"
echo
echo -e "${BLUE}💡 Development Commands:${NC}"
echo -e "   • npm run dev     - Start with nodemon"
echo -e "   • npm test        - Run tests"
echo -e "   • npm run lint    - Check code style"
echo -e "   • ./scripts/test.sh - Run integration tests"
echo

# nodemon으로 개발 서버 시작
if command -v nodemon >/dev/null 2>&1; then
    echo -e "${GREEN}🔄 Starting with nodemon (auto-reload)${NC}"
    nodemon server.js
else
    echo -e "${GREEN}▶️  Starting with node${NC}"
    node server.js
fi