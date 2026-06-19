<#
.SYNOPSIS
    Builds and runs the MySecondBrain E2E regression tests.
.DESCRIPTION
    This script:
    1. Builds the entire solution (Debug|Any CPU)
    2. Starts the E2E tests using dotnet test
    3. Reports results

    Requirements:
    - .NET 8.0 SDK
    - Solution must be built before running tests
#>

$ErrorActionPreference = "Stop"
$SolutionDir = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$TestProject = Join-Path $PSScriptRoot "MySecondBrain.Tests.E2E"

Write-Host "=== MySecondBrain E2E Tests ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the solution
Write-Host "[1/2] Building solution..." -ForegroundColor Yellow
dotnet build $SolutionDir\MySecondBrain.sln `
    --configuration Debug `
    --nologo `
    --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED. Aborting." -ForegroundColor Red
    exit 1
}

Write-Host "Build succeeded." -ForegroundColor Green
Write-Host ""

# Step 2: Run E2E tests
Write-Host "[2/2] Running E2E tests..." -ForegroundColor Yellow
Write-Host "NOTE: These tests launch the real WPF application." -ForegroundColor Magenta
Write-Host "      Do not interact with the keyboard/mouse during execution." -ForegroundColor Magenta
Write-Host ""

dotnet test $TestProject `
    --configuration Debug `
    --no-build `
    --nologo `
    --verbosity normal `
    --logger "console;verbosity=detailed"

$exitCode = $LASTEXITCODE

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "=== ALL E2E TESTS PASSED ===" -ForegroundColor Green
} else {
    Write-Host "=== SOME E2E TESTS FAILED (exit code: $exitCode) ===" -ForegroundColor Red
}

exit $exitCode
