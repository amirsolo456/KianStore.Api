param(
    [Parameter(Mandatory = $false)]
    [string]$BaseUrl = 'http://localhost:5069'
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

function Test-Get($Path) {
    $uri = "$BaseUrl$Path"
    try {
        $response = Invoke-WebRequest -Uri $uri -Method Get -UseBasicParsing -TimeoutSec 20
        Write-Host "PASS $($response.StatusCode) $Path" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "FAIL $Path : $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

$paths = @(
    '/api/health',
    '/api/products?page=1&pageSize=5',
    '/api/customers?page=1&pageSize=5',
    '/api/reference/warehouses',
    '/api/reference/accounts',
    '/api/reference/parties?page=1&pageSize=5',
    '/api/reference/users',
    '/api/reference/document-types?idSal=1405',
    '/api/stock?page=1&pageSize=5&idAnbar=1&idSal=1405',
    '/api/discount-codes?activeOnly=true',
    '/api/sms/status',
    '/api/sms/templates?activeOnly=true'
)

$failed = 0
foreach ($path in $paths) {
    if (-not (Test-Get $path)) { $failed++ }
}

Write-Host ""
if ($failed -eq 0) {
    Write-Host 'API smoke test passed.' -ForegroundColor Green
    exit 0
}

Write-Host "$failed API checks failed." -ForegroundColor Red
exit 1
