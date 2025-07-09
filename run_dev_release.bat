@echo off
echo ==========================================
echo   Blokus Online Server - Development (Release)
echo ==========================================

echo Setting up development environment variables...

REM Database Configuration
set DB_HOST=localhost
set DB_PORT=5432
set DB_USER=admin
set DB_PASSWORD=admin
set DB_NAME=blokus_online
set DB_POOL_SIZE=10

REM Server Configuration
set SERVER_PORT=7777
set SERVER_MAX_CLIENTS=1000
set SERVER_THREAD_POOL_SIZE=4

REM Security Configuration
set JWT_SECRET=dev_secret_key_12345_change_in_production
set SESSION_TIMEOUT_HOURS=24
set PASSWORD_SALT_ROUNDS=12

REM Game Configuration
set GAME_MAX_PLAYERS_PER_ROOM=4
set GAME_MIN_PLAYERS_TO_START=2
set GAME_TURN_TIMEOUT_SECONDS=120

REM Logging Configuration
set LOG_LEVEL=info
set LOG_DIRECTORY=logs
set LOG_FILE_MAX_SIZE=10485760
set LOG_MAX_FILES=5

REM Development Settings
set DEBUG_MODE=false
set ENABLE_SQL_LOGGING=false

echo Environment variables set successfully!
echo.
echo Starting Blokus Server (Release mode)...
echo.

build\server\Release\BlokusServer.exe

echo.
echo Server stopped.
pause