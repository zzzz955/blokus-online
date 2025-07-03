# simple_diagnose.ps1 - 간단한 링커 문제 진단

Write-Host "Diagnosing linker problem..." -ForegroundColor Green

# 1. vcpkg_installed 구조 확인
Write-Host "`nChecking vcpkg_installed structure..." -ForegroundColor Cyan
$vcpkgPath = "vcpkg_installed\x64-windows"

if (Test-Path $vcpkgPath) {
    Write-Host "vcpkg_installed exists" -ForegroundColor Green
    
    # Debug 라이브러리들
    $libDebugPath = "$vcpkgPath\debug\lib"
    Write-Host "`nDebug libraries:" -ForegroundColor Yellow
    if (Test-Path $libDebugPath) {
        Get-ChildItem -Path $libDebugPath -Filter "*protobuf*" | ForEach-Object {
            Write-Host "  $($_.Name)" -ForegroundColor White
        }
    } else {
        Write-Host "  No debug lib directory" -ForegroundColor Red
    }
    
    # Release 라이브러리들
    $libReleasePath = "$vcpkgPath\lib"
    Write-Host "`nRelease libraries:" -ForegroundColor Yellow
    if (Test-Path $libReleasePath) {
        Get-ChildItem -Path $libReleasePath -Filter "*protobuf*" | ForEach-Object {
            Write-Host "  $($_.Name)" -ForegroundColor White
        }
    } else {
        Write-Host "  No release lib directory" -ForegroundColor Red
    }
    
    # DLL 확인
    $binDebugPath = "$vcpkgPath\debug\bin"
    Write-Host "`nDebug DLLs:" -ForegroundColor Yellow
    if (Test-Path $binDebugPath) {
        Get-ChildItem -Path $binDebugPath -Filter "*.dll" | ForEach-Object {
            Write-Host "  $($_.Name)" -ForegroundColor White
        }
    } else {
        Write-Host "  No debug bin directory" -ForegroundColor Red
    }
    
} else {
    Write-Host "vcpkg_installed does not exist" -ForegroundColor Red
}

# 2. 빌드 디렉터리 확인
Write-Host "`nChecking build directories..." -ForegroundColor Cyan
$buildDirs = @("build", "out")
foreach ($dir in $buildDirs) {
    if (Test-Path $dir) {
        Write-Host "$dir exists" -ForegroundColor Green
        $exeFiles = Get-ChildItem -Path $dir -Filter "*.exe" -Recurse
        foreach ($exe in $exeFiles) {
            Write-Host "  EXE: $($exe.FullName)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "$dir does not exist" -ForegroundColor Red
    }
}

Write-Host "`nDiagnosis complete!" -ForegroundColor Green