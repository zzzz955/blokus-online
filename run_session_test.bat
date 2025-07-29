@echo off
echo 🧪 세션 정리 시스템 테스트 실행
echo ================================

echo 📦 컴파일 중...
g++ -std=c++17 -O2 test_session_cleanup.cpp -o session_test.exe

if %ERRORLEVEL% NEQ 0 (
    echo ❌ 컴파일 실패
    pause
    exit /b 1
)

echo ✅ 컴파일 성공
echo.
echo 🚀 테스트 실행 중...
echo ⚠️  주의: 이 테스트는 실제로 5분 이상 소요됩니다.
echo.

session_test.exe

echo.
echo 🧹 정리 중...
del session_test.exe

echo ✅ 테스트 완료
pause