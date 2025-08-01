@echo off
REM Blokus Online - Production Environment Setup
REM 배포 환경용 환경변수 설정

echo Setting up production environment variables...

REM 배포용 서버 설정
set BLOKUS_SERVER_HOST=blokus-online.mooo.com
set BLOKUS_SERVER_PORT=9999

echo BLOKUS_SERVER_HOST=%BLOKUS_SERVER_HOST%
echo BLOKUS_SERVER_PORT=%BLOKUS_SERVER_PORT%

echo.
echo Production environment variables set!
echo You can now run the client with these settings.
echo.

REM 클라이언트 실행 (선택사항)
REM build\Release\BlokusClient.exe