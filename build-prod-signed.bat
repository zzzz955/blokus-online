@echo off
echo Starting signed production build...
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

REM Certificate check
echo.
echo [INFO] Checking certificate...
powershell -Command "& { $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like '*BlokusOnline*' -or $_.Subject -like '*Code Signing*' } | Select-Object -First 1; if ($cert) { Write-Host '[OK] Certificate found: ' $cert.Subject -ForegroundColor Green; Write-Host '    Thumbprint: ' $cert.Thumbprint -ForegroundColor Cyan; $env:CODE_SIGN_THUMBPRINT = $cert.Thumbprint } else { Write-Host '[ERROR] Code signing certificate not found.' -ForegroundColor Red; Write-Host '        Please run generate-certificate.ps1 first.' -ForegroundColor Yellow; exit 1 } }"

if %errorlevel% neq 0 (
    echo.
    echo Certificate generation needed. Please run generate-certificate.ps1 first.
    pause
    exit /b 1
)

REM CMake build
if not exist "build\CMakeCache.txt" (
    echo No existing build found, configuring from scratch...
    cmake --preset production
)

echo.
echo [INFO] Building BlokusClient...
cmake --build build --config Release --target BlokusClient --parallel 4
if %errorlevel% neq 0 (
    echo Build failed, trying full reconfigure...
    cmake --preset production
    cmake --build build --config Release --target BlokusClient --parallel 4
)

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed. Try manually deleting 'build' folder and run again.
    pause
    exit /b 1
)

REM Code signing after successful build
echo.
echo [INFO] Code signing...
set EXE_PATH=build\client\Release\BlokusClient.exe

REM Execute code signing in PowerShell
powershell -Command "& { $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like '*BlokusOnline*' -or $_.Subject -like '*Code Signing*' } | Select-Object -First 1; if ($cert) { try { $result = Set-AuthenticodeSignature -FilePath '%EXE_PATH%' -Certificate $cert -TimestampServer 'http://timestamp.digicert.com'; if ($result.Status -eq 'Valid') { Write-Host '[OK] Code signing completed!' -ForegroundColor Green } else { Write-Host '[WARN] Code signing status: ' $result.Status -ForegroundColor Yellow } } catch { Write-Host '[ERROR] Code signing failed: ' $_.Exception.Message -ForegroundColor Red } } else { Write-Host '[ERROR] Certificate not found.' -ForegroundColor Red } }"

REM Signature verification
echo.
echo [INFO] Verifying signature...
powershell -Command "& { $sig = Get-AuthenticodeSignature '%EXE_PATH%'; Write-Host 'Signature Status: ' $sig.Status; if ($sig.SignerCertificate) { Write-Host 'Signer: ' $sig.SignerCertificate.Subject } }"

echo.
echo [OK] Signed build completed successfully!
echo Executable: %EXE_PATH%
echo.
echo Notes:
echo   - Windows Defender warnings may still appear on first run
echo   - Self-signed certificates do not provide complete trust
echo   - Users may need to click "More info" - "Run anyway"
echo.
pause