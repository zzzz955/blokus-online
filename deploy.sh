#!/bin/bash
# Unified Deployment Script for Blokus Online
# Uses pre-built images from GitHub Container Registry

echo " Blokus Online Unified Deployment"
echo "===================================="

# Git 변경사항 확인
CHANGED_FILES=$(git diff --name-only HEAD~1)
echo "📁 Changed files:"
echo "$CHANGED_FILES"
echo ""

# 서비스 배포 플래그
DEPLOY_WEB=false
DEPLOY_SERVER=false
DEPLOY_NGINX=false

# 변경된 파일에 따라 배포 대상 결정
if echo "$CHANGED_FILES" | grep -q "^web/"; then
    DEPLOY_WEB=true
    echo " Web changes detected"
fi

if echo "$CHANGED_FILES" | grep -q "^server/\|^common/\|^proto/"; then
    DEPLOY_SERVER=true
    echo "🎮 Server changes detected"
fi

if echo "$CHANGED_FILES" | grep -q "^nginx/\|^docker-compose.yml"; then
    DEPLOY_NGINX=true
    echo "⚙️ Infrastructure changes detected"
fi

# 변경사항 없으면 전체 배포
if [ "$DEPLOY_WEB" = false ] && [ "$DEPLOY_SERVER" = false ] && [ "$DEPLOY_NGINX" = false ]; then
    echo "❓ No specific changes detected, deploying all services"
    DEPLOY_WEB=true
    DEPLOY_SERVER=true
    DEPLOY_NGINX=true
fi

echo ""
echo " Deployment Plan:"
echo "==================="
[ "$DEPLOY_WEB" = true ] && echo " Web Service (blokus-web)"
[ "$DEPLOY_SERVER" = true ] && echo " Game Server (blokus-server)"  
[ "$DEPLOY_NGINX" = true ] && echo " Nginx (blokus-nginx)"
echo ""

# 이미지 태그 설정 (기본값: latest, 환경변수로 SHA 기반 태그 사용 가능)
IMAGE_TAG=${IMAGE_TAG:-latest}
echo "🏷️ Using image tag: $IMAGE_TAG"

# GitHub Container Registry 로그인 확인
if ! docker info | grep -q "Registry: https://ghcr.io"; then
    echo "⚠️ Please login to GitHub Container Registry first:"
    echo "   echo \$GITHUB_TOKEN | docker login ghcr.io -u YOUR_USERNAME --password-stdin"
fi

# 배포 실행 (사전 빌드된 이미지 사용)
if [ "$DEPLOY_SERVER" = true ]; then
    echo "🎮 Deploying Game Server with image tag: $IMAGE_TAG..."
    GAME_SERVER_IMAGE="ghcr.io/zzzz955/blokus-online/blokus-game-server:$IMAGE_TAG" \
    docker-compose pull blokus-server
    docker-compose up -d blokus-server
    echo " Game Server deployed"
fi

if [ "$DEPLOY_WEB" = true ]; then
    echo " Deploying Web Service with image tag: $IMAGE_TAG..."
    WEB_SERVER_IMAGE="ghcr.io/zzzz955/blokus-online/blokus-web-server:$IMAGE_TAG" \
    docker-compose pull blokus-web
    docker-compose up -d blokus-web
    echo " Web Service deployed"
fi

if [ "$DEPLOY_NGINX" = true ]; then
    echo "⚙️ Restarting Nginx..."
    docker-compose restart nginx
    echo " Nginx restarted"
fi

# SSL 인증서 확인 (첫 배포 시)
DOMAIN="blokus-online.mooo.com"
if [ ! -f "./certbot/conf/live/$DOMAIN/fullchain.pem" ]; then
    echo ""
    echo "🔐 SSL certificate not found. Setting up SSL..."
    echo "⚠️  SSL setup requires manual action:"
    echo "   Run: bash ./scripts/init-ssl.sh"
    echo "   (Make sure to update EMAIL in the script first)"
fi

echo ""
echo "🎉 Deployment completed!"
echo " Service Status:"
docker-compose ps

# 간단한 헬스체크
echo ""
echo "🩺 Health Check:"
sleep 10  # 서비스 시작 대기
if docker-compose ps --filter health=healthy | grep -q healthy; then
    echo " Services are healthy"
else
    echo "⚠️ Some services may still be starting or have issues"
    echo "Run 'docker-compose logs' to check details"
fi