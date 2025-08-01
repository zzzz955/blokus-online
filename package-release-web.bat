@echo off
echo Creating web-ready release package...

set BUILD_DIR=build\client\Release
set EXE_NAME=BlokusClient.exe
set WEB_DOWNLOADS_DIR=web\public\downloads
set RELEASES_DIR=releases

REM 버전 정보 입력 받기
set /p VERSION="Enter version (e.g., 1.0.0): "
if "%VERSION%"=="" set VERSION=1.0.0

echo.
echo Version: %VERSION%
echo Build Directory: %BUILD_DIR%
echo Web Downloads: %WEB_DOWNLOADS_DIR%
echo Local Archive: %RELEASES_DIR%
echo.

REM 빌드 파일 존재 확인
if not exist "%BUILD_DIR%\%EXE_NAME%" (
    echo ❌ Error: %EXE_NAME% not found in %BUILD_DIR%
    echo Please build the project first using build-prod-fast.bat
    pause
    exit /b 1
)

REM 디렉토리 생성
echo 📁 Creating directories...
if not exist "%WEB_DOWNLOADS_DIR%" mkdir "%WEB_DOWNLOADS_DIR%"
if not exist "%RELEASES_DIR%" mkdir "%RELEASES_DIR%"

REM 임시 패키징 디렉토리
set TEMP_DIR=temp_package
if exist %TEMP_DIR% rmdir /s /q %TEMP_DIR%
mkdir %TEMP_DIR%

echo 📦 Packaging essential files...
copy "%BUILD_DIR%\BlokusClient.exe" "%TEMP_DIR%\" >nul
copy "%BUILD_DIR%\Qt5*.dll" "%TEMP_DIR%\" >nul
copy "%BUILD_DIR%\libprotobuf.dll" "%TEMP_DIR%\" >nul
copy "%BUILD_DIR%\libssl*.dll" "%TEMP_DIR%\" >nul
copy "%BUILD_DIR%\libcrypto*.dll" "%TEMP_DIR%\" >nul
copy "%BUILD_DIR%\*.dll" "%TEMP_DIR%\" >nul

echo 🔌 Creating plugin directories...
mkdir "%TEMP_DIR%\plugins\platforms" >nul
mkdir "%TEMP_DIR%\plugins\imageformats" >nul

copy "%BUILD_DIR%\plugins\platforms\qwindows.dll" "%TEMP_DIR%\plugins\platforms\" >nul
copy "%BUILD_DIR%\plugins\imageformats\qico.dll" "%TEMP_DIR%\plugins\imageformats\" >nul
copy "%BUILD_DIR%\plugins\imageformats\qjpeg.dll" "%TEMP_DIR%\plugins\imageformats\" >nul

REM 웹용 최신 버전 생성
echo 🌐 Creating web version: BlokusClient-latest.zip
if exist "%WEB_DOWNLOADS_DIR%\BlokusClient-latest.zip" (
    echo   Removing existing web version...
    del "%WEB_DOWNLOADS_DIR%\BlokusClient-latest.zip"
)
powershell -command "Compress-Archive -Path '%TEMP_DIR%\*' -DestinationPath '%WEB_DOWNLOADS_DIR%\BlokusClient-latest.zip' -Force"

REM 로컬 히스토리용 버전별 아카이브 생성
echo 📚 Creating local archive: BlokusClient-v%VERSION%.zip
set LOCAL_ARCHIVE=%RELEASES_DIR%\BlokusClient-v%VERSION%.zip
if exist "%LOCAL_ARCHIVE%" (
    echo   Archive already exists, creating backup...
    move "%LOCAL_ARCHIVE%" "%LOCAL_ARCHIVE%.backup.%date:~-4,4%%date:~-10,2%%date:~-7,2%"
)
powershell -command "Compress-Archive -Path '%TEMP_DIR%\*' -DestinationPath '%LOCAL_ARCHIVE%' -Force"

REM 버전 정보 JSON 생성
echo 📝 Updating version information...
set VERSION_JSON=%WEB_DOWNLOADS_DIR%\version.json
powershell -command "$version = @{ version = '%VERSION%'; releaseDate = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); downloadUrl = '/api/download/client'; fileSize = (Get-Item '%WEB_DOWNLOADS_DIR%\BlokusClient-latest.zip').Length; changelog = @('Version %VERSION% released', 'Latest features and bug fixes', 'Improved stability and performance') }; $version | ConvertTo-Json -Depth 3 | Out-File -FilePath '%VERSION_JSON%' -Encoding UTF8"

REM 임시 디렉토리 정리
rmdir /s /q %TEMP_DIR%

REM 결과 출력
echo.
echo ✅ Release package created successfully!
echo.
echo 🌐 Web Version (for download API):
echo    File: %WEB_DOWNLOADS_DIR%\BlokusClient-latest.zip
for %%F in ("%WEB_DOWNLOADS_DIR%\BlokusClient-latest.zip") do echo    Size: %%~zF bytes (%.2f MB)
echo    URL:  /api/download/client
echo.
echo 📚 Local Archive (for history):
echo    File: %LOCAL_ARCHIVE%
for %%F in ("%LOCAL_ARCHIVE%") do echo    Size: %%~zF bytes (%.2f MB)
echo.
echo 📝 Version Info:
echo    File: %VERSION_JSON%
echo    Version: %VERSION%
echo.
echo 🎯 Next Steps:
echo    1. Test download: http://localhost:3000/api/download/client
echo    2. Commit to Git: git add %WEB_DOWNLOADS_DIR%/ && git commit -m "Update client to v%VERSION%"
echo    3. Push to deploy: git push
echo.
pause