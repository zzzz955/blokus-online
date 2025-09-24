#!/bin/bash

# ==================================================
# SSL 인증서 자동 갱신 스크립트
# Let's Encrypt 인증서 갱신 및 nginx 재시작
# ==================================================

set -e

echo "=== SSL 인증서 갱신 시작 ==="

# 현재 디렉토리 확인
if [ ! -f "docker-compose.yml" ]; then
  echo " docker-compose.yml 파일을 찾을 수 없습니다."
  echo "프로젝트 루트 디렉토리에서 실행해 주세요."
  exit 1
fi

# 인증서 만료일 확인
DOMAIN="blokus-online.mooo.com"
CERT_PATH="./certbot/conf/live/$DOMAIN/fullchain.pem"

if [ -f "$CERT_PATH" ]; then
  EXPIRY_DATE=$(openssl x509 -enddate -noout -in "$CERT_PATH" | cut -d= -f2)
  EXPIRY_TIMESTAMP=$(date -d "$EXPIRY_DATE" +%s)
  CURRENT_TIMESTAMP=$(date +%s)
  DAYS_UNTIL_EXPIRY=$(( ($EXPIRY_TIMESTAMP - $CURRENT_TIMESTAMP) / 86400 ))
  
  echo "📅 현재 인증서 만료까지: $DAYS_UNTIL_EXPIRY 일"
  
  if [ $DAYS_UNTIL_EXPIRY -gt 30 ]; then
    echo " 인증서가 아직 유효합니다. (30일 이상 남음)"
    echo "강제 갱신하려면 --force 옵션을 사용하세요."
    if [ "$1" != "--force" ]; then
      exit 0
    fi
  fi
else
  echo "⚠️ 기존 인증서를 찾을 수 없습니다."
  echo "초기 설정을 위해 scripts/init-ssl.sh를 먼저 실행해 주세요."
  exit 1
fi

# 인증서 갱신 시도
echo " 인증서 갱신 중..."
docker-compose run --rm certbot renew --quiet

# 갱신 성공 여부 확인
if [ $? -eq 0 ]; then
  echo " 인증서 갱신 완료"
  
  # nginx 설정 테스트
  echo " nginx 설정 테스트 중..."
  if docker-compose exec nginx nginx -t; then
    echo " nginx 설정이 올바릅니다."
    
    # nginx 재시작
    echo " nginx 재시작 중..."
    docker-compose restart nginx
    
    if [ $? -eq 0 ]; then
      echo " nginx 재시작 완료"
      
      # 새 인증서 정보 확인
      NEW_EXPIRY_DATE=$(openssl x509 -enddate -noout -in "$CERT_PATH" | cut -d= -f2)
      echo "📋 새 인증서 만료일: $NEW_EXPIRY_DATE"
      
      echo "🎉 SSL 인증서 갱신이 성공적으로 완료되었습니다!"
    else
      echo " nginx 재시작에 실패했습니다."
      exit 1
    fi
  else
    echo " nginx 설정에 오류가 있습니다."
    exit 1
  fi
else
  echo " 인증서 갱신에 실패했습니다."
  echo " 로그를 확인해 주세요:"
  echo "   docker-compose logs certbot"
  exit 1
fi

echo ""
echo "=== SSL 인증서 갱신 완료 ==="