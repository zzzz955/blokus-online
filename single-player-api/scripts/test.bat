@echo off
REM Blokus Single Player API 테스트 스크립트 (Windows)

setlocal EnableDelayedExpansion

REM 환경 변수 설정
if "%API_BASE_URL%"=="" set API_BASE_URL=http://localhost:8080/api
if "%TEST_JWT_TOKEN%"=="" set TEST_JWT_TOKEN=

echo.
echo 🧪 Blokus Single Player API Test Suite
echo =====================================
echo 🌐 Testing API at: %API_BASE_URL%
if "%TEST_JWT_TOKEN%"=="" (
    echo 🔑 JWT Token: Not provided
) else (
    echo 🔑 JWT Token: Provided
)
echo.

REM Node.js 테스트 실행
echo 📋 Running automated tests...
echo.
if exist "test\api-test.js" (
    call node test\api-test.js
    if %errorlevel% neq 0 (
        echo ❌ Automated tests failed
        goto :manual_tests
    )
) else (
    echo ❌ Test file not found: test\api-test.js
    goto :manual_tests
)

:manual_tests
echo.
echo 🔍 Manual cURL tests...
echo.

REM cURL이 설치되어 있는지 확인
where curl >nul 2>&1
if %errorlevel% neq 0 (
    echo ⚠️  cURL not found. Using PowerShell for HTTP requests...
    goto :powershell_tests
)

REM 기본 헬스체크 테스트
echo Testing health check...
curl -s -o nul -w "Status: %%{http_code}\n" "%API_BASE_URL%/health" 2>nul
if %errorlevel% neq 0 echo ❌ Health check failed

echo.

REM 라이브니스 체크 테스트
echo Testing liveness...
curl -s -o nul -w "Status: %%{http_code}\n" "%API_BASE_URL%/health/live" 2>nul
if %errorlevel% neq 0 echo ❌ Liveness check failed

echo.

REM 레디니스 체크 테스트
echo Testing readiness...
curl -s -o nul -w "Status: %%{http_code}\n" "%API_BASE_URL%/health/ready" 2>nul
if %errorlevel% neq 0 echo ❌ Readiness check failed

goto :end_tests

:powershell_tests
REM PowerShell을 사용한 HTTP 테스트
echo Testing with PowerShell...

powershell -Command "try { $response = Invoke-RestMethod -Uri '%API_BASE_URL%/health' -Method Get; Write-Host 'Health Check: OK'; } catch { Write-Host 'Health Check: Failed' -ForegroundColor Red; }"

powershell -Command "try { $response = Invoke-RestMethod -Uri '%API_BASE_URL%/health/live' -Method Get; Write-Host 'Liveness Check: OK'; } catch { Write-Host 'Liveness Check: Failed' -ForegroundColor Red; }"

powershell -Command "try { $response = Invoke-RestMethod -Uri '%API_BASE_URL%/health/ready' -Method Get; Write-Host 'Readiness Check: OK'; } catch { Write-Host 'Readiness Check: Failed' -ForegroundColor Red; }"

:end_tests
echo.
echo ✅ Manual tests completed
echo.
echo 💡 Testing Tips:
echo    • For full testing, provide JWT_TOKEN: set TEST_JWT_TOKEN=your-token-here
echo    • Test with Docker: docker-compose up -d ^&^& npm run test:integration
echo    • Check logs: docker-compose logs blokus-single-api
echo    • Monitor Redis: docker-compose exec redis redis-cli monitor
echo.
pause