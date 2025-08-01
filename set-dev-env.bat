@echo off
REM Blokus Online - Development Environment Setup
REM 개발 환경용 환경변수 설정

echo Setting up development environment variables...

REM 개발용 로컬 서버 설정
set BLOKUS_SERVER_HOST=localhost
set BLOKUS_SERVER_PORT=9999

echo BLOKUS_SERVER_HOST=%BLOKUS_SERVER_HOST%
echo BLOKUS_SERVER_PORT=%BLOKUS_SERVER_PORT%

echo.
echo Development environment variables set!
echo You can now run the client with these settings.
echo.

REM 클라이언트 실행 (선택사항)
REM build\Debug\BlokusClient.exe