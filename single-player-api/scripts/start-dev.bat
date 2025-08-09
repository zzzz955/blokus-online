@echo off
REM Blokus Single Player API 개발 환경 시작 스크립트 (Windows)

echo.
echo 🚀 Blokus Single Player API - Development Setup
echo ==============================================

REM 환경 파일 확인
if not exist ".env" (
    echo ⚠️  .env file not found. Creating from .env.example...
    if exist ".env.example" (
        copy ".env.example" ".env" >nul
        echo ✅ .env file created. Please edit it with your configuration.
    ) else (
        echo ❌ .env.example not found. Please create .env file manually.
        pause
        exit /b 1
    )
)

REM Node.js 버전 확인
echo.
echo 📋 Checking Node.js version...
node --version 2>nul
if %errorlevel% neq 0 (
    echo ❌ Node.js is not installed. Please install Node.js 18 or higher.
    pause
    exit /b 1
)

REM npm 패키지 설치
echo.
echo 📦 Installing dependencies...
if not exist "node_modules" (
    call npm install
    if %errorlevel% neq 0 (
        echo ❌ Failed to install dependencies.
        pause
        exit /b 1
    )
) else (
    echo ✅ Dependencies already installed
)

REM 로그 디렉터리 생성
echo.
echo 📁 Creating logs directory...
if not exist "logs" mkdir logs

REM Docker Compose 서비스 시작 확인
echo.
echo 🐳 Checking database services...
docker --version >nul 2>&1
if %errorlevel% equ 0 (
    echo    Starting PostgreSQL and Redis...
    docker-compose up -d postgres redis 2>nul
    if %errorlevel% equ 0 (
        echo ✅ Database services started
        timeout /t 5 /nobreak >nul
    ) else (
        echo ⚠️  Could not start database services. Make sure Docker is running.
    )
) else (
    echo ⚠️  Docker not found. Make sure PostgreSQL and Redis are running manually.
)

REM 개발 서버 시작
echo.
echo 🌟 Starting development server...
echo ✅ Setup completed!
echo.
echo 📍 Server will be available at:
echo    🌐 API: http://localhost:8080/api
echo    ❤️  Health: http://localhost:8080/api/health
echo    📚 Docs: http://localhost:8080/api
echo.
echo 💡 Development Commands:
echo    • npm run dev     - Start with nodemon
echo    • npm test        - Run tests
echo    • npm run lint    - Check code style
echo    • npm run test:api - Run API tests
echo.

REM nodemon으로 개발 서버 시작
where nodemon >nul 2>&1
if %errorlevel% equ 0 (
    echo 🔄 Starting with nodemon (auto-reload)
    call npx nodemon server.js
) else (
    echo ▶️  Starting with node
    call node server.js
)

pause