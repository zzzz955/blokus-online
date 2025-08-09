#!/bin/bash

# Blokus Single Player API 테스트 스크립트
# 이 스크립트는 API 서버의 기본 기능을 테스트합니다.

set -e

# 색상 설정
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 환경 변수 설정
API_BASE_URL=${API_BASE_URL:-"http://localhost:8080/api"}
TEST_JWT_TOKEN=${TEST_JWT_TOKEN:-""}

echo -e "${BLUE}🧪 Blokus Single Player API Test Suite${NC}"
echo -e "${BLUE}=====================================${NC}"
echo -e "🌐 Testing API at: ${API_BASE_URL}"
echo -e "🔑 JWT Token: ${TEST_JWT_TOKEN:+Provided}" "${TEST_JWT_TOKEN:-Not provided}"
echo

# Node.js 테스트 실행
echo -e "${YELLOW}📋 Running automated tests...${NC}"
echo

if [ -f "test/api-test.js" ]; then
    node test/api-test.js
else
    echo -e "${RED}❌ Test file not found: test/api-test.js${NC}"
    exit 1
fi

echo
echo -e "${BLUE}🔍 Manual cURL tests...${NC}"
echo

# 기본 헬스체크 테스트
echo -e "${YELLOW}Testing health check...${NC}"
curl -s -o /dev/null -w "Status: %{http_code}\n" "${API_BASE_URL}/health" || echo "❌ Health check failed"

echo

# 라이브니스 체크 테스트
echo -e "${YELLOW}Testing liveness...${NC}"
curl -s -o /dev/null -w "Status: %{http_code}\n" "${API_BASE_URL}/health/live" || echo "❌ Liveness check failed"

echo

# 레디니스 체크 테스트
echo -e "${YELLOW}Testing readiness...${NC}"
curl -s -o /dev/null -w "Status: %{http_code}\n" "${API_BASE_URL}/health/ready" || echo "❌ Readiness check failed"

echo

# API 루트 테스트
echo -e "${YELLOW}Testing API root...${NC}"
curl -s "${API_BASE_URL}/" | head -c 100
echo

# JWT 토큰이 제공된 경우 인증된 엔드포인트 테스트
if [ ! -z "$TEST_JWT_TOKEN" ]; then
    echo
    echo -e "${YELLOW}Testing authenticated endpoints...${NC}"
    
    # 토큰 검증
    echo -e "Token validation:"
    curl -s -X POST \
         -H "Authorization: Bearer ${TEST_JWT_TOKEN}" \
         -H "Content-Type: application/json" \
         "${API_BASE_URL}/auth/validate" | head -c 200
    echo
    
    # 스테이지 데이터 조회
    echo -e "Stage data (Stage 1):"
    curl -s -H "Authorization: Bearer ${TEST_JWT_TOKEN}" \
         "${API_BASE_URL}/stages/1" | head -c 200
    echo
    
    # 사용자 프로필 조회
    echo -e "User profile:"
    curl -s -H "Authorization: Bearer ${TEST_JWT_TOKEN}" \
         "${API_BASE_URL}/user/profile" | head -c 200
    echo
else
    echo
    echo -e "${YELLOW}⚠️  No JWT token provided. Skipping authenticated endpoint tests.${NC}"
    echo -e "   Set TEST_JWT_TOKEN environment variable to test authenticated endpoints."
fi

echo
echo -e "${YELLOW}Testing unauthenticated access (should return 401)...${NC}"
curl -s -o /dev/null -w "Status: %{http_code}\n" "${API_BASE_URL}/stages/1" || echo "Expected 401 status"

echo
echo -e "${YELLOW}Testing 404 endpoint...${NC}"
curl -s -o /dev/null -w "Status: %{http_code}\n" "${API_BASE_URL}/nonexistent" || echo "Expected 404 status"

echo
echo -e "${GREEN}✅ Manual tests completed${NC}"
echo
echo -e "${BLUE}💡 Testing Tips:${NC}"
echo -e "   • For full testing, provide JWT_TOKEN: export TEST_JWT_TOKEN='your-token-here'"
echo -e "   • Test with Docker: docker-compose up -d && ./scripts/test.sh"
echo -e "   • Check logs: docker-compose logs blokus-single-api"
echo -e "   • Monitor Redis: docker-compose exec redis redis-cli monitor"