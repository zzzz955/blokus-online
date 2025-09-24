#!/bin/bash

# ==================================================
# SSL 인증서 초기 설정 스크립트
# Let's Encrypt를 사용한 HTTPS 인증서 발급
# ==================================================

set -e

# 설정 변수
DOMAIN="blokus-online.mooo.com"
EMAIL="your-email@example.com"  # 실제 이메일로 변경 필요
DATA_PATH="./certbot"

echo "=== Blokus Online SSL 인증서 초기 설정 ==="

# 기존 인증서 확인
if [ -d "$DATA_PATH/conf/live/$DOMAIN" ]; then
  echo "⚠️ 기존 인증서가 발견되었습니다."
  read -p "기존 인증서를 삭제하고 새로 발급하시겠습니까? (y/N) " -n 1 -r
  echo
  if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "기존 인증서 삭제 중..."
    sudo rm -rf "$DATA_PATH/conf/live/$DOMAIN"
    sudo rm -rf "$DATA_PATH/conf/archive/$DOMAIN"
    sudo rm -rf "$DATA_PATH/conf/renewal/$DOMAIN.conf"
  else
    echo " 기존 인증서를 유지합니다."
    exit 0
  fi
fi

# 필수 디렉토리 생성
echo "📁 필수 디렉토리 생성 중..."
mkdir -p "$DATA_PATH/conf"
mkdir -p "$DATA_PATH/www"
mkdir -p "./logs/nginx"

# 임시 nginx 설정으로 HTTP 검증 준비
echo " 임시 nginx 설정 준비 중..."
cat > ./nginx/nginx-temp.conf << 'EOF'
events {
    worker_connections 1024;
}

http {
    server {
        listen 80;
        server_name blokus-online.mooo.com;
        
        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
        }
        
        location / {
            return 200 "Temporary server for SSL setup";
            add_header Content-Type text/plain;
        }
    }
}
EOF

# 임시 nginx 컨테이너 실행
echo " 임시 nginx 컨테이너 실행 중..."
docker run -d --name temp-nginx \
  -p 80:80 \
  -v $(pwd)/nginx/nginx-temp.conf:/etc/nginx/nginx.conf:ro \
  -v $(pwd)/certbot/www:/var/www/certbot:ro \
  nginx:alpine

# 도메인 연결 확인
echo " 도메인 연결 확인 중..."
if ! curl -s http://$DOMAIN/.well-known/acme-challenge/test > /dev/null; then
  echo "⚠️ 도메인 연결을 확인할 수 없습니다. DNS 설정을 확인해 주세요."
  echo "http://$DOMAIN 에 접근할 수 있는지 확인해 주세요."
  read -p "계속 진행하시겠습니까? (y/N) " -n 1 -r
  echo
  if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    docker stop temp-nginx && docker rm temp-nginx
    exit 1
  fi
fi

# Let's Encrypt 인증서 발급
echo "🔐 SSL 인증서 발급 중..."
docker run --rm \
  -v $(pwd)/certbot/conf:/etc/letsencrypt \
  -v $(pwd)/certbot/www:/var/www/certbot \
  certbot/certbot \
  certonly --webroot \
    -w /var/www/certbot \
    --email $EMAIL \
    -d $DOMAIN \
    --agree-tos \
    --no-eff-email \
    --staging  # 테스트용 - 성공하면 --staging 제거

# 임시 nginx 컨테이너 정리
echo "🧹 임시 컨테이너 정리 중..."
docker stop temp-nginx && docker rm temp-nginx
rm -f ./nginx/nginx-temp.conf

# 인증서 발급 확인
if [ -d "$DATA_PATH/conf/live/$DOMAIN" ]; then
  echo " SSL 인증서가 성공적으로 발급되었습니다!"
  echo "📁 인증서 위치: $DATA_PATH/conf/live/$DOMAIN/"
  
  # 인증서 정보 출력
  echo "📋 인증서 정보:"
  sudo openssl x509 -in "$DATA_PATH/conf/live/$DOMAIN/fullchain.pem" -text -noout | grep -A 2 "Validity"
  
  echo ""
  echo " 이제 다음 명령으로 전체 서비스를 시작할 수 있습니다:"
  echo "   docker-compose up -d"
  echo ""
  echo "⚠️ staging 인증서로 발급되었습니다. 실제 운영 시에는:"
  echo "   1. 스크립트에서 --staging 옵션 제거"
  echo "   2. EMAIL 변수를 실제 이메일로 변경"
  echo "   3. 스크립트 재실행"
  
else
  echo " SSL 인증서 발급에 실패했습니다."
  echo " 다음 사항을 확인해 주세요:"
  echo "   1. 도메인이 올바르게 설정되어 있는지"
  echo "   2. 80번 포트가 열려있고 접근 가능한지"
  echo "   3. 방화벽 설정이 올바른지"
  exit 1
fi

echo ""
echo "=== SSL 설정 완료 ==="