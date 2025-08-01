# Self-Signed Certificate Generator for Blokus Online
# Run PowerShell as Administrator

param(
    [string]$CompanyName = "BlokusOnline",
    [string]$CertStore = "Cert:\CurrentUser\My"
)

# Console encoding setup
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=== Blokus Online Self-Signed Certificate Generator ===" -ForegroundColor Green
Write-Host ""

# Check for existing certificate
$existingCert = Get-ChildItem $CertStore | Where-Object { $_.Subject -like "*$CompanyName*" }
if ($existingCert) {
    Write-Host "Existing certificate found:" -ForegroundColor Yellow
    Write-Host "  Subject: $($existingCert.Subject)" -ForegroundColor Yellow
    Write-Host "  Thumbprint: $($existingCert.Thumbprint)" -ForegroundColor Yellow
    Write-Host "  Expires: $($existingCert.NotAfter)" -ForegroundColor Yellow
    Write-Host ""
    
    $choice = Read-Host "Use existing certificate? (Y/N)"
    if ($choice -eq "Y" -or $choice -eq "y") {
        Write-Host "Using existing certificate." -ForegroundColor Green
        Write-Host "Thumbprint: $($existingCert.Thumbprint)" -ForegroundColor Cyan
        return $existingCert.Thumbprint
    }
}

try {
    Write-Host "Creating new self-signed certificate..." -ForegroundColor Cyan
    
    # Create certificate
    $cert = New-SelfSignedCertificate `
        -DnsName $CompanyName `
        -Type CodeSigning `
        -CertStoreLocation $CertStore `
        -Subject "CN=$CompanyName Code Signing" `
        -NotAfter (Get-Date).AddYears(3) `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256

    Write-Host "Certificate created successfully!" -ForegroundColor Green
    Write-Host "  Subject: $($cert.Subject)" -ForegroundColor White
    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Cyan
    Write-Host "  Expires: $($cert.NotAfter)" -ForegroundColor White
    Write-Host ""
    
    # Add certificate to trusted root store (optional)
    $choice = Read-Host "Add certificate to Trusted Root Certification Authorities? (Recommended) (Y/N)"
    if ($choice -eq "Y" -or $choice -eq "y") {
        try {
            # Copy certificate to trusted root store
            $rootStore = "Cert:\CurrentUser\Root"
            $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
            $rootCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
            $rootCert.Import($certBytes)
            
            $store = Get-Item $rootStore
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $store.Add($rootCert)
            $store.Close()
            
            Write-Host "Certificate added to Trusted Root Certification Authorities." -ForegroundColor Green
        }
        catch {
            Write-Host "Failed to add to Trusted Root store: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "   Please manually add the certificate to Trusted Root store." -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "Usage Instructions:" -ForegroundColor Yellow
    Write-Host "  1. Use this Thumbprint in build scripts: $($cert.Thumbprint)" -ForegroundColor White
    Write-Host "  2. Or set environment variable: `$env:CODE_SIGN_THUMBPRINT='$($cert.Thumbprint)'" -ForegroundColor White
    Write-Host ""
    
    return $cert.Thumbprint
}
catch {
    Write-Host "Certificate creation failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Run PowerShell as Administrator" -ForegroundColor White
    Write-Host "  2. Enable Windows Developer Mode" -ForegroundColor White
    Write-Host "  3. Check execution policy: Get-ExecutionPolicy" -ForegroundColor White
    Write-Host "  4. Set execution policy if needed: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor White
    throw
}