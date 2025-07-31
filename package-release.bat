@echo off
echo Creating release package (packaging only, no build)...

set RELEASE_DIR=BlokusClient-latest
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

echo Copying essential files only (optimized)...
copy "%BUILD_DIR%\BlokusClient.exe" "%RELEASE_DIR%\" 2>nul
copy "%BUILD_DIR%\Qt5*.dll" "%RELEASE_DIR%\" 2>nul
copy "%BUILD_DIR%\libprotobuf.dll" "%RELEASE_DIR%\" 2>nul
copy "%BUILD_DIR%\libssl*.dll" "%RELEASE_DIR%\" 2>nul
copy "%BUILD_DIR%\libcrypto*.dll" "%RELEASE_DIR%\" 2>nul
copy "%BUILD_DIR%\*.dll" "%RELEASE_DIR%\" 2>nul

echo Creating essential plugin directories...
mkdir "%RELEASE_DIR%\plugins\platforms" 2>nul
mkdir "%RELEASE_DIR%\plugins\imageformats" 2>nul

copy "%BUILD_DIR%\plugins\platforms\qwindows.dll" "%RELEASE_DIR%\plugins\platforms\" 2>nul
copy "%BUILD_DIR%\plugins\imageformats\qico.dll" "%RELEASE_DIR%\plugins\imageformats\" 2>nul
copy "%BUILD_DIR%\plugins\imageformats\qjpeg.dll" "%RELEASE_DIR%\plugins\imageformats\" 2>nul

REM 선택적 파일들 (필요시 주석 해제)
REM copy "%BUILD_DIR%\plugins\imageformats\qgif.dll" "%RELEASE_DIR%\plugins\imageformats\" 2>nul
REM copy "%BUILD_DIR%\plugins\imageformats\qsvg.dll" "%RELEASE_DIR%\plugins\imageformats\" 2>nul

echo.
echo Release package created in: %RELEASE_DIR%
echo Contents:
dir /b %RELEASE_DIR%

echo.
echo Creating ZIP archive...
set ZIP_NAME=BlokusClient-latest.zip
powershell -command "Compress-Archive -Path '%RELEASE_DIR%\*' -DestinationPath '%ZIP_NAME%' -Force"

if exist "%ZIP_NAME%" (
    echo.
    echo ✅ ZIP archive created: %ZIP_NAME%
    for %%F in ("%ZIP_NAME%") do echo Archive size: %%~zF bytes
    echo.
    echo 📁 Folder: %RELEASE_DIR%
    echo 📦 Archive: %ZIP_NAME%
    echo.
    echo Ready for distribution!
) else (
    echo.
    echo ❌ Failed to create ZIP archive
    echo Please check if PowerShell is available
)
pause