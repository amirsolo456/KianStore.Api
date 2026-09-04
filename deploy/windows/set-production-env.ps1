param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,
    [string]$SmsSendUrl = '',
    [string]$SmsApiKey = '',
    [string]$SmsSender = '',
    [string]$SmsProvider = 'HttpSmsProvider'
)

# Run as Administrator. Values are stored as machine-level environment variables
# so secrets do not need to be committed to Git.
$ErrorActionPreference = 'Stop'

$variables = @{
    'ASPNETCORE_ENVIRONMENT' = 'Production'
    'ConnectionStrings__KianStore' = $ConnectionString
    'Sms__SendUrl' = $SmsSendUrl
    'Sms__ApiKey' = $SmsApiKey
    'Sms__Sender' = $SmsSender
    'Sms__Provider' = $SmsProvider
}

foreach ($item in $variables.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($item.Key, $item.Value, 'Machine')
    Write-Host "Configured $($item.Key)" -ForegroundColor Green
}

Write-Host 'Production environment variables saved.' -ForegroundColor Green
Write-Host 'Restart the IIS application pool after changing these values.' -ForegroundColor Yellow
