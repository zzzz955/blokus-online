#!/bin/bash
# Smart Deployment Script for Blokus Online

echo "🚀 Blokus Online Smart Deployment"
echo "================================="

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
    echo "🌐 Web changes detected"
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
echo "🎯 Deployment Plan:"
echo "==================="
[ "$DEPLOY_WEB" = true ] && echo "✅ Web Service (blokus-web)"
[ "$DEPLOY_SERVER" = true ] && echo "✅ Game Server (blokus-server)"  
[ "$DEPLOY_NGINX" = true ] && echo "✅ Nginx (blokus-nginx)"
echo ""

# 배포 실행
if [ "$DEPLOY_SERVER" = true ]; then
    echo "🎮 Deploying Game Server..."
    docker-compose up -d --build blokus-server
    echo "✅ Game Server deployed"
fi

if [ "$DEPLOY_WEB" = true ]; then
    echo "🌐 Deploying Web Service..."
    docker-compose up -d --build blokus-web
    echo "✅ Web Service deployed"
fi

if [ "$DEPLOY_NGINX" = true ]; then
    echo "⚙️ Restarting Nginx..."
    docker-compose restart nginx
    echo "✅ Nginx restarted"
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
echo "📊 Service Status:"
docker-compose ps