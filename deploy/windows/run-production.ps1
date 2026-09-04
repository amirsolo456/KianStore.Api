param(
    [int]$Port = 5000
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Exe = Join-Path $Root 'KianStore.Api.exe'
$Dll = Join-Path $Root 'KianStore.Api.dll'

# The release artifact is self-contained/single-file. For backwards compatibility,
# the script can also run the framework-dependent DLL when it is present.
if (Test-Path $Exe) {
    $Application = $Exe
    $UseDotNet = $false
}
elseif (Test-Path $Dll) {
    $Application = $Dll
    $UseDotNet = $true
}
else {
    throw "Published API executable not found in $Root"
}

$env:ASPNETCORE_ENVIRONMENT = 'Production'
# Bind to all interfaces so Flutter clients can reach the API from another machine.
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"

Write-Host "Starting KianStore.Api on http://0.0.0.0:$Port" -ForegroundColor Cyan
Write-Host "Health (server): http://127.0.0.1:$Port/api/health" -ForegroundColor Cyan
Write-Host "Health (network): http://<SERVER-IP>:$Port/api/health" -ForegroundColor Cyan

Set-Location $Root
if ($UseDotNet) {
    & dotnet $Application
}
else {
    & $Application
}
