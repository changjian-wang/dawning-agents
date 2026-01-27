# Build project
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Clean,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot/../../..

try {
    if ($Clean) {
        Write-Host "Cleaning..." -ForegroundColor Cyan
        dotnet clean --nologo -v q
    }

    Write-Host "Building ($Configuration)..." -ForegroundColor Cyan

    $args = @("build", "--nologo", "-c", $Configuration)
    if ($Quiet) {
        $args += "-v"
        $args += "q"
    }

    & dotnet @args

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✅ Build succeeded!" -ForegroundColor Green
    } else {
        Write-Host "`n❌ Build failed!" -ForegroundColor Red
        exit 1
    }
}
finally {
    Pop-Location
}
