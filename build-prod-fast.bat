@echo off
echo Starting production build...
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

if not exist "build\CMakeCache.txt" (
    echo No existing build found, configuring from scratch...
    cmake --preset production
)

echo Building BlokusClient...
cmake --build build --config Release --target BlokusClient --parallel 4
if %errorlevel% neq 0 (
    echo Build failed, trying full reconfigure...
    cmake --preset production
    cmake --build build --config Release --target BlokusClient --parallel 4
)

if %errorlevel% equ 0 (
    echo.
    echo ✅ Build completed successfully!
    echo Executable: build\client\Release\BlokusClient.exe
) else (
    echo.
    echo ❌ Build failed. Try manually deleting 'build' folder and run again.
)
pause