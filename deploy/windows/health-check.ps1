param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl
)

$ErrorActionPreference = 'Stop'
$ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
$HealthUrl = "$ApiBaseUrl/api/health"

Write-Host "Checking $HealthUrl ..." -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 15

if ($response.StatusCode -ne 200) {
    throw "Health check returned HTTP $($response.StatusCode)."
}

Write-Host 'API health check passed.' -ForegroundColor Green
Write-Host $response.Content
