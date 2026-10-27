# Gives an existing account admin rights (local development).
# The person must have registered on the website and verified their email first.
# Usage:  powershell -ExecutionPolicy Bypass -File .\make-admin.ps1 you@example.com
#         add -SqlServer when the site runs with run-local.ps1 (SQL Server + real Firebase)
param([Parameter(Mandatory = $true)][string]$Email, [switch]$SqlServer)

$env:ASPNETCORE_ENVIRONMENT     = 'Development'
# With -SqlServer the database settings come from appsettings.Development.json.
if (-not $SqlServer) {
    $env:Database__Provider         = 'Sqlite'
    $env:ConnectionStrings__Default = 'Data Source=App_Data/dev.db'
    $env:Firebase__ProjectId        = 'demo-antique-store'
    $env:Firebase__AuthEmulatorHost = '127.0.0.1:9099'
}

Set-Location (Join-Path $PSScriptRoot 'backend\AntiqueStore.Api')
# --no-build lets this run while the backend is running (the backend has already built the app).
dotnet run --no-build --no-launch-profile -- admin grant $Email
if ($LASTEXITCODE -eq 0) {
    Write-Host 'Done. Refresh the website (or sign out and in again), then open http://localhost:5173/admin' -ForegroundColor Green
}
