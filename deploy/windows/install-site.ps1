param(
    [Parameter(Mandatory = $true)]
    [string]$PhysicalPath,
    [string]$SiteName = 'KianStore.Api',
    [string]$AppPoolName = 'KianStore.Api',
    [int]$Port = 80,
    [string]$HostName = ''
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

if (-not (Test-Path $PhysicalPath)) {
    throw "Publish directory not found: $PhysicalPath"
}

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}

Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.loadUserProfile -Value $true

if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    if ([string]::IsNullOrWhiteSpace($HostName)) {
        New-Website -Name $SiteName -PhysicalPath $PhysicalPath -Port $Port -ApplicationPool $AppPoolName | Out-Null
    }
    else {
        New-Website -Name $SiteName -PhysicalPath $PhysicalPath -Port $Port -HostHeader $HostName -ApplicationPool $AppPoolName | Out-Null
    }
}
else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
}

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName

Write-Host "IIS site '$SiteName' is ready at: http://$HostName`:$Port" -ForegroundColor Green
Write-Host 'Add the final HTTPS/443 binding and certificate before public production use.' -ForegroundColor Yellow
