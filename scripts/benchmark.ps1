#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run Dawning.Agents benchmarks.

.DESCRIPTION
    Runs performance benchmarks using BenchmarkDotNet.
    Results are saved to benchmarks/results directory.

.PARAMETER Filter
    Benchmark filter pattern (e.g., "*Memory*", "*Token*")

.PARAMETER Job
    Job type: Short, Medium, Long (default: Short)

.EXAMPLE
    ./scripts/benchmark.ps1

.EXAMPLE
    ./scripts/benchmark.ps1 -Filter "*Memory*"

.EXAMPLE
    ./scripts/benchmark.ps1 -Job Long
#>

param(
    [string]$Filter = "*",

    [ValidateSet("Short", "Medium", "Long")]
    [string]$Job = "Short"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Dawning.Agents Benchmark Runner" -ForegroundColor Cyan
Write-Host "  Filter: $Filter" -ForegroundColor Cyan
Write-Host "  Job: $Job" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build in Release mode
Write-Host "Building in Release mode..." -ForegroundColor Yellow
dotnet build benchmarks/Dawning.Agents.Benchmarks -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Create results directory
$resultsDir = "benchmarks/results"
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
}

# Run benchmarks
Write-Host ""
Write-Host "Running benchmarks..." -ForegroundColor Yellow
$jobArg = switch ($Job) {
    "Short" { "--job short" }
    "Medium" { "--job medium" }
    "Long" { "--job long" }
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$artifactsPath = "$resultsDir/$timestamp"

Push-Location benchmarks/Dawning.Agents.Benchmarks
try {
    dotnet run -c Release --no-build -- --filter "$Filter" $jobArg --artifacts $artifactsPath
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Benchmarks Complete!" -ForegroundColor Green
Write-Host "  Results: $artifactsPath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
