# Run all tests
param(
    [string]$Filter = "",
    [switch]$Coverage
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot/../../..

try {
    Write-Host "Running tests..." -ForegroundColor Cyan

    $args = @("test", "--nologo")

    if ($Filter) {
        $args += "--filter"
        $args += "FullyQualifiedName~$Filter"
        Write-Host "  Filter: $Filter" -ForegroundColor Gray
    }

    if ($Coverage) {
        $args += '--collect:"XPlat Code Coverage"'
        Write-Host "  Coverage: enabled" -ForegroundColor Gray
    }

    & dotnet @args

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✅ All tests passed!" -ForegroundColor Green
    } else {
        Write-Host "`n❌ Some tests failed!" -ForegroundColor Red
        exit 1
    }
}
finally {
    Pop-Location
}
