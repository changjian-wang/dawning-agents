#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pack all Dawning.Agents NuGet packages locally.

.DESCRIPTION
    This script builds and packs all Dawning.Agents packages for local testing
    or manual publishing to NuGet.org.

.PARAMETER Version
    The version number for the packages (e.g., 0.1.0)

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER OutputPath
    Output directory for .nupkg files. Default: ./nupkgs

.EXAMPLE
    ./scripts/pack.ps1 -Version 0.1.0

.EXAMPLE
    ./scripts/pack.ps1 -Version 0.2.0-preview.1 -Configuration Debug
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputPath = "./nupkgs"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Dawning.Agents NuGet Pack Script" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean output directory
if (Test-Path $OutputPath) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Update version in Directory.Build.props
Write-Host "Updating version to $Version..." -ForegroundColor Yellow
$propsPath = "Directory.Build.props"
$content = Get-Content $propsPath -Raw
$content = $content -replace '<Version>.*</Version>', "<Version>$Version</Version>"
Set-Content -Path $propsPath -Value $content -NoNewline

# Restore
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

# Build
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Test
Write-Host "Running tests..." -ForegroundColor Yellow
dotnet test -c $Configuration --no-build --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

# Pack each project
$projects = @(
    "src/Dawning.Agents.Abstractions/Dawning.Agents.Abstractions.csproj",
    "src/Dawning.Agents.Core/Dawning.Agents.Core.csproj",
    "src/Dawning.Agents.OpenAI/Dawning.Agents.OpenAI.csproj",
    "src/Dawning.Agents.Azure/Dawning.Agents.Azure.csproj",
    "src/Dawning.Agents.Redis/Dawning.Agents.Redis.csproj",
    "src/Dawning.Agents.Qdrant/Dawning.Agents.Qdrant.csproj",
    "src/Dawning.Agents.Pinecone/Dawning.Agents.Pinecone.csproj"
)

Write-Host ""
Write-Host "Packing NuGet packages..." -ForegroundColor Yellow
foreach ($project in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host "  Packing $name..." -ForegroundColor Gray
    dotnet pack $project -c $Configuration -o $OutputPath --no-build
    if ($LASTEXITCODE -ne 0) { throw "Pack failed for $name" }
}

# List generated packages
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Generated Packages:" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Get-ChildItem $OutputPath -Filter "*.nupkg" | ForEach-Object {
    $size = [math]::Round($_.Length / 1KB, 1)
    Write-Host "  $($_.Name) ($size KB)" -ForegroundColor White
}

Write-Host ""
Write-Host "Done! Packages are in: $OutputPath" -ForegroundColor Green
Write-Host ""
Write-Host "To publish to NuGet.org:" -ForegroundColor Yellow
Write-Host "  dotnet nuget push `"$OutputPath/*.nupkg`" --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json" -ForegroundColor Gray
