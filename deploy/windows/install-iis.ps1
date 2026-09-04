# Run in an elevated PowerShell window on Windows Server.
$ErrorActionPreference = 'Stop'

Write-Host 'Installing IIS...' -ForegroundColor Cyan
Install-WindowsFeature Web-Server, Web-Common-Http, Web-Default-Doc, Web-Static-Content, Web-Http-Errors, Web-Http-Logging, Web-Request-Monitor, Web-Http-Redirect, Web-Filtering, Web-Mgmt-Console

Write-Host 'IIS installation completed.' -ForegroundColor Green
Write-Host 'Next: install the matching .NET 10 Hosting Bundle before creating the IIS site.' -ForegroundColor Yellow
