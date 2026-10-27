# Starts the whole store locally WITHOUT Docker, using SQL Server (view it in SSMS) and a real Firebase project:
#   1. Backend API (SQL Server database "AntiqueStore")   http://localhost:5080   (API docs: /docs)
#   2. Frontend (Vite)                                   http://localhost:5173   (admin panel: /admin)
# The database settings come from backend/AntiqueStore.Api/appsettings.Development.json. Migrations are NOT applied
# on startup: run "dotnet run -- db migrate" once first. Full setup: docs/LOCAL-SETUP.md
# Usage (from the project folder):  powershell -ExecutionPolicy Bypass -File .\run-local.ps1
$root = $PSScriptRoot

# Stop a backend that is already running, so port 5080 is free.
Get-Process AntiqueStore.Api -ErrorAction SilentlyContinue | Stop-Process -Force

# Firebase:ProjectId and Firebase:CredentialsPath come from dotnet user-secrets (kept outside the repo).
Start-Process powershell -ArgumentList '-NoExit', '-Command', @"
Set-Location '$root\backend\AntiqueStore.Api'
`$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --no-launch-profile --urls http://localhost:5080
"@

Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$root\frontend'; if (-not (Test-Path node_modules)) { npm install }; npm run dev -- --port 5173"

Write-Host 'Starting... open http://localhost:5173 in about 30-60 seconds.'
