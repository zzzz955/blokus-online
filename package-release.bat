@echo off
echo Creating release package (packaging only, no build)...

set RELEASE_DIR=release-package
set BUILD_DIR=build\client\Release
set EXE_NAME=BlokusClient.exe

if not exist "%BUILD_DIR%\%EXE_NAME%" (
    echo Error: %EXE_NAME% not found in %BUILD_DIR%
    echo Please build the project first using production build.
    pause
    exit /b 1
)

echo Cleaning release directory...
if exist %RELEASE_DIR% rmdir /s /q %RELEASE_DIR%
mkdir %RELEASE_DIR%

echo Copying executable...
copy "%BUILD_DIR%\%EXE_NAME%" "%RELEASE_DIR%\"

echo Running windeployqt to gather dependencies...
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

REM Qt deployment tool이 vcpkg에서 설치된 Qt를 찾도록 설정
set QT_DIR=%CD%\vcpkg_installed\x64-windows\tools\Qt5
set PATH=%QT_DIR%\bin;%PATH%

REM windeployqt 실행 (Qt DLL과 플러그인 자동 복사)
windeployqt.exe --release --no-translations --no-system-d3d-compiler --no-opengl-sw "%RELEASE_DIR%\%EXE_NAME%"

echo.
echo Release package created in: %RELEASE_DIR%
echo Contents:
dir /b %RELEASE_DIR%
echo.
echo Ready for distribution!
pause