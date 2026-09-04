# Run from any PowerShell location on a Windows machine with the .NET 10 SDK installed.
$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$PublishDir = Join-Path $Root 'deploy\windows\publish'

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

Write-Host "Publishing KianStore.Api from $Root" -ForegroundColor Cyan

dotnet restore (Join-Path $Root 'KianStore.Api.csproj')
dotnet publish (Join-Path $Root 'KianStore.Api.csproj') `
    --configuration Release `
    --output $PublishDir `
    --no-restore

Write-Host "Publish completed: $PublishDir" -ForegroundColor Green
