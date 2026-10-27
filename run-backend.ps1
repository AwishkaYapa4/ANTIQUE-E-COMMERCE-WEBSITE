# Starts the backend API for local development.
#   API:      http://localhost:5080
#   API docs: http://localhost:5080/docs
# Usage:  powershell -ExecutionPolicy Bypass -File .\run-backend.ps1
#         (or double-click run-backend.cmd)
$ErrorActionPreference = 'Stop'
$api = Join-Path $PSScriptRoot 'backend\AntiqueStore.Api'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET SDK is not installed. Install .NET 10 from https://dotnet.microsoft.com/download' -ForegroundColor Red
    exit 1
}

# Stop a backend that is already running, so port 5080 is free.
Get-Process AntiqueStore.Api -ErrorAction SilentlyContinue | Stop-Process -Force

$env:ASPNETCORE_ENVIRONMENT    = 'Development'
$env:Database__Provider        = 'Sqlite'
$env:ConnectionStrings__Default = 'Data Source=App_Data/dev.db'
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:Database__SeedExampleData = 'true'
$env:Firebase__ProjectId       = 'demo-antique-store'
$env:Firebase__AuthEmulatorHost = '127.0.0.1:9099'

Set-Location $api
New-Item -ItemType Directory -Force 'App_Data' | Out-Null

Write-Host ''
Write-Host 'Starting backend... (first start can take 1-2 minutes)' -ForegroundColor Cyan
Write-Host 'When you see "Now listening on: http://localhost:5080" it is ready.' -ForegroundColor Cyan
Write-Host 'Check: http://localhost:5080/health/ready   Docs: http://localhost:5080/docs' -ForegroundColor Cyan
Write-Host 'Press Ctrl+C to stop.' -ForegroundColor Cyan
Write-Host ''

dotnet run --no-launch-profile --urls http://localhost:5080
