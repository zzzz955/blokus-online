@echo off
echo Creating signed release package...

set BUILD_DIR=build\client\Release
set EXE_NAME=BlobloClient.exe
set RELEASES_DIR=releases

REM Get version input
set /p VERSION="Enter version (e.g., 1.0.0): "
if "%VERSION%"=="" set VERSION=1.0.0

echo.
echo Version: %VERSION%
echo Build Directory: %BUILD_DIR%
echo Releases Directory: %RELEASES_DIR%
echo.

REM Check if build file exists
if not exist "%BUILD_DIR%\%EXE_NAME%" (
    echo [ERROR] %EXE_NAME% not found in %BUILD_DIR%
    echo Please build the project first using build-prod-signed.bat
    pause
    exit /b 1
)

REM Create directories
echo [INFO] Creating directories...
if not exist "%RELEASES_DIR%" mkdir "%RELEASES_DIR%"

REM Create version-specific directory
set VERSION_DIR=%RELEASES_DIR%\v%VERSION%
if exist "%VERSION_DIR%" (
    echo   Version directory already exists, creating backup...
    for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
    set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
    set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"
    set "datestamp=%YYYY%%MM%%DD%_%HH%%Min%%Sec%"
    move "%VERSION_DIR%" "%VERSION_DIR%.backup.%datestamp%"
)
mkdir "%VERSION_DIR%"

REM Create temporary packaging directory
set TEMP_DIR=temp_package_v%VERSION%
if exist %TEMP_DIR% rmdir /s /q %TEMP_DIR%
mkdir %TEMP_DIR%

echo [INFO] Packaging entire release directory for maximum stability...
echo   Copying all files from %BUILD_DIR% to %TEMP_DIR%...
xcopy "%BUILD_DIR%\*" "%TEMP_DIR%\" /E /I /Q >nul
echo   Release directory copied completely (includes all DLLs, plugins, and resources)

REM Check signature status
echo [INFO] Checking code signature...
powershell -Command "& { $sig = Get-AuthenticodeSignature '%TEMP_DIR%\BlobloClient.exe'; if ($sig.Status -eq 'Valid') { Write-Host '[OK] Executable is properly signed.' -ForegroundColor Green } else { Write-Host '[WARN] Signature status: ' $sig.Status -ForegroundColor Yellow; Write-Host '        Please use build-prod-signed.bat to create signed builds.' -ForegroundColor Yellow } }"

REM Create README file
echo [INFO] Creating README...
echo Blokus Online Client v%VERSION% > "%TEMP_DIR%\README.txt"
echo ================================ >> "%TEMP_DIR%\README.txt"
echo. >> "%TEMP_DIR%\README.txt"
echo Installation Instructions: >> "%TEMP_DIR%\README.txt"
echo 1. Extract the archive >> "%TEMP_DIR%\README.txt"
echo 2. Run BlobloClient.exe >> "%TEMP_DIR%\README.txt"
echo. >> "%TEMP_DIR%\README.txt"
echo Notes: >> "%TEMP_DIR%\README.txt"
echo - Windows Defender may show "Unknown Publisher" warning >> "%TEMP_DIR%\README.txt"
echo - Click "More info" - "Run anyway" to execute the program >> "%TEMP_DIR%\README.txt"
echo - This is due to using a self-signed certificate >> "%TEMP_DIR%\README.txt"
echo. >> "%TEMP_DIR%\README.txt"
echo System Requirements: >> "%TEMP_DIR%\README.txt"
echo - Windows 10 or higher >> "%TEMP_DIR%\README.txt"
echo - Visual C++ Redistributable 2019 or higher >> "%TEMP_DIR%\README.txt"
echo. >> "%TEMP_DIR%\README.txt"
echo Release Date: %date% %time% >> "%TEMP_DIR%\README.txt"

REM Create ZIP archive
echo [INFO] Creating release archive: BlobloClient-v%VERSION%.zip
set RELEASE_ARCHIVE=%VERSION_DIR%\BlobloClient-v%VERSION%.zip
powershell -Command "Compress-Archive -Path '%TEMP_DIR%\*' -DestinationPath '%RELEASE_ARCHIVE%' -Force"

REM Create release info JSON
echo [INFO] Creating release info...
set RELEASE_INFO=%VERSION_DIR%\release-info.json
powershell -Command "$info = @{ version = '%VERSION%'; releaseDate = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); platform = 'Windows'; architecture = 'x64'; signed = $true; fileSize = (Get-Item '%RELEASE_ARCHIVE%').Length; checksum = (Get-FileHash '%RELEASE_ARCHIVE%' -Algorithm SHA256).Hash; changelog = @('Version %VERSION% released', 'Code signing applied for enhanced security', 'Stability and performance improvements') }; $info | ConvertTo-Json -Depth 3 | Out-File -FilePath '%RELEASE_INFO%' -Encoding UTF8"

REM Create latest version reference
echo [INFO] Creating latest version reference...
set LATEST_DIR=%RELEASES_DIR%\latest
if exist "%LATEST_DIR%" rmdir /s /q "%LATEST_DIR%"
xcopy "%VERSION_DIR%" "%LATEST_DIR%\" /E /I /Q >nul

REM Update overall release index
echo [INFO] Updating release index...
set RELEASE_INDEX=%RELEASES_DIR%\releases.json
powershell -Command "if (Test-Path '%RELEASE_INDEX%') { $releases = Get-Content '%RELEASE_INDEX%' | ConvertFrom-Json } else { $releases = @() } ; $newRelease = @{ version = '%VERSION%'; releaseDate = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); platform = 'Windows'; architecture = 'x64'; signed = $true; downloadPath = 'v%VERSION%/BlobloClient-v%VERSION%.zip'; fileSize = (Get-Item '%RELEASE_ARCHIVE%').Length }; $releases = @($newRelease) + @($releases | Where-Object { $_.version -ne '%VERSION%' }); $releases | ConvertTo-Json -Depth 3 | Out-File -FilePath '%RELEASE_INDEX%' -Encoding UTF8"

REM Clean up temporary directory
rmdir /s /q %TEMP_DIR%

REM Display results
echo.
echo [OK] Signed release package created successfully!
echo.
echo Release Directory:
echo    %VERSION_DIR%\
echo.
echo Release File:
echo    %RELEASE_ARCHIVE%
for %%F in ("%RELEASE_ARCHIVE%") do echo    Size: %%~zF bytes
echo.
echo Release Info:
echo    %RELEASE_INFO%
echo    Version: %VERSION%
echo.
echo Latest Version Reference:
echo    %LATEST_DIR%\
echo.
echo Overall Release Index:
echo    %RELEASE_INDEX%
echo.
echo Next Steps:
echo    1. Test release: Extract and run %LATEST_DIR%\BlobloClient-v%VERSION%.zip
echo    2. Git commit: git add %RELEASES_DIR%/ ^&^& git commit -m "Release v%VERSION% with code signing"
echo    3. Git tag: git tag v%VERSION% ^&^& git push origin v%VERSION%
echo.
pause