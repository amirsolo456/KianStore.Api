param(
    [int]$Port = 5070
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
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"

Write-Host "Starting KianStore.Api on http://127.0.0.1:$Port" -ForegroundColor Cyan
Write-Host "Health: http://127.0.0.1:$Port/api/health" -ForegroundColor Cyan

Set-Location $Root
if ($UseDotNet) {
    & dotnet $Application
}
else {
    & $Application
}
