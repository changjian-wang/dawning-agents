# Pre-commit checks
param(
    [switch]$SkipFormat
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot/../../..

try {
    Write-Host "Running pre-commit checks..." -ForegroundColor Cyan
    Write-Host ""

    # 1. Build
    Write-Host "1. Building project..." -ForegroundColor Yellow
    dotnet build --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "   ✅ Build succeeded" -ForegroundColor Green

    # 2. Test
    Write-Host "2. Running tests..." -ForegroundColor Yellow
    dotnet test --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Tests failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "   ✅ All tests passed" -ForegroundColor Green

    # 3. Format (optional)
    if (-not $SkipFormat) {
        Write-Host "3. Checking format..." -ForegroundColor Yellow
        dotnet csharpier . --check
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   ⚠️ Formatting issues found, fixing..." -ForegroundColor Yellow
            dotnet csharpier .
        }
        Write-Host "   ✅ Format OK" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "✅ All pre-commit checks passed!" -ForegroundColor Green
    Write-Host "Ready to commit." -ForegroundColor Cyan
}
finally {
    Pop-Location
}
