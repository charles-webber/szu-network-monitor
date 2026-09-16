[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$helperOutput = Join-Path $artifacts 'helper'
$publishOutput = Join-Path $artifacts 'publish'

New-Item -ItemType Directory -Force -Path $helperOutput, $publishOutput | Out-Null

Push-Location (Join-Path $root 'src\SzuLoginHelper')
try {
    go test ./...
    go build -trimpath -ldflags='-s -w' -o (Join-Path $helperOutput 'szu-login-helper.exe') .
}
finally {
    Pop-Location
}

dotnet publish (Join-Path $root 'src\SZUNetworkMonitor\SZUNetworkMonitor.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $publishOutput

Copy-Item -LiteralPath (Join-Path $helperOutput 'szu-login-helper.exe') -Destination (Join-Path $publishOutput 'szu-login-helper.exe') -Force
Write-Host "Portable application staged in: $publishOutput"

if (Get-Command iscc.exe -ErrorAction SilentlyContinue) {
    & iscc.exe (Join-Path $root 'installer\SZUNetworkMonitor.iss')
    Write-Host "Installer staged in: $artifacts"
}
else {
    Write-Warning 'Inno Setup was not found. The portable application was built, but no installer was created.'
}
