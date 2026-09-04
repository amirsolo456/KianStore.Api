param(
    [int]$Port = 5070
)

$ErrorActionPreference = 'Stop'
$PublishRoot = (Resolve-Path (Join-Path $PSScriptRoot 'publish')).Path
$Dll = Join-Path $PublishRoot 'KianStore.Api.dll'

if (-not (Test-Path $Dll)) {
    throw "Published API not found: $Dll"
}

$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"

# The production appsettings file already points to .\SQL2025 / KianStore_2.
# A machine-level ConnectionStrings__KianStore value can override it when needed.

Write-Host "Starting KianStore.Api on http://127.0.0.1:$Port" -ForegroundColor Cyan
Write-Host "Health: http://127.0.0.1:$Port/api/health" -ForegroundColor Cyan

Set-Location $PublishRoot
dotnet .\KianStore.Api.dll
