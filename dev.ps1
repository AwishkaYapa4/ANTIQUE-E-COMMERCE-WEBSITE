# Starts the whole store locally in three windows (no MySQL or Firebase account needed):
#   1. Firebase Auth Emulator (local sign-in)   http://127.0.0.1:9099
#   2. Backend API (SQLite dev database)         http://localhost:5080   (API docs: /docs)
#   3. Frontend (Vite)                           http://localhost:5173
# Usage (from the project folder):  powershell -ExecutionPolicy Bypass -File .\dev.ps1
$root = $PSScriptRoot

Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$root'; npx -y firebase-tools@14 emulators:start --only auth --project demo-antique-store"

Start-Process powershell -ArgumentList '-NoExit', '-Command', @"
Set-Location '$root\backend\AntiqueStore.Api'
`$env:ASPNETCORE_ENVIRONMENT='Development'
`$env:Database__Provider='Sqlite'
`$env:ConnectionStrings__Default='Data Source=App_Data/dev.db'
`$env:Database__ApplyMigrationsOnStartup='true'
`$env:Database__SeedExampleData='true'
`$env:Firebase__ProjectId='demo-antique-store'
`$env:Firebase__AuthEmulatorHost='127.0.0.1:9099'
dotnet run --no-launch-profile --urls http://localhost:5080
"@

Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$root\frontend'; if (-not (Test-Path node_modules)) { npm install }; npx vite --mode e2e --port 5173"

Write-Host 'Starting... open http://localhost:5173 in about 30-60 seconds.'
