#Requires -Version 5.1
<#
.SYNOPSIS
    Installs or uninstalls qcompact, a WSL 2 disk compaction tool.

.DESCRIPTION
    Downloads the qcompact.exe release asset from GitHub, verifies its SHA256
    checksum, installs it to a per-user directory, and adds that directory to
    the current user's PATH.

.PARAMETER Version
    Release version to install, e.g. 1.0.0. Defaults to the latest release.

.PARAMETER InstallDir
    Destination directory. Defaults to %LOCALAPPDATA%\Programs\qcompact.

.PARAMETER Uninstall
    Remove the install directory and its PATH entry.

.EXAMPLE
    iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing | iex

.EXAMPLE
    .\install.ps1 -Version 1.0.0

.EXAMPLE
    .\install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$Version = 'latest',
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\qcompact'),
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProgressPreference = 'SilentlyContinue'

$Repo = 'Keeferf/qcompact'
$ExeName = 'qcompact.exe'

if ($env:OS -ne 'Windows_NT') {
    throw 'qcompact runs on Windows only.'
}

function Get-UserPath {
    $value = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ([string]::IsNullOrEmpty($value)) { return @() }
    return @($value -split ';' | Where-Object { $_ -ne '' })
}

function Set-UserPath {
    param([string[]]$Entries)
    [Environment]::SetEnvironmentVariable('Path', ($Entries -join ';'), 'User')
}

function Add-ToUserPath {
    param([string]$Directory)
    $entries = Get-UserPath
    if ($entries -notcontains $Directory) {
        Set-UserPath (@($entries) + $Directory)
        return $true
    }
    return $false
}

function Remove-FromUserPath {
    param([string]$Directory)
    $entries = Get-UserPath
    $kept = @($entries | Where-Object { $_.TrimEnd('\') -ne $Directory.TrimEnd('\') })
    if ($kept.Count -ne $entries.Count) {
        Set-UserPath $kept
        return $true
    }
    return $false
}

if ($Uninstall) {
    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
        Write-Host "Removed $InstallDir"
    }
    else {
        Write-Host "Nothing to remove at $InstallDir"
    }

    if (Remove-FromUserPath -Directory $InstallDir) {
        Write-Host 'Removed the install directory from your PATH.'
    }

    Write-Host 'Open a new terminal for the PATH change to take effect.'
    return
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if ($Version -eq 'latest') {
    $baseUrl = "https://github.com/$Repo/releases/latest/download"
}
else {
    $baseUrl = "https://github.com/$Repo/releases/download/v$($Version.TrimStart('v'))"
}

$tempExe = Join-Path ([IO.Path]::GetTempPath()) "qcompact-$([guid]::NewGuid().ToString('N')).exe"
$tempSha = "$tempExe.sha256"

try {
    Write-Host "Downloading $ExeName ($Version)..."
    Invoke-WebRequest -Uri "$baseUrl/$ExeName" -OutFile $tempExe -UseBasicParsing
    Invoke-WebRequest -Uri "$baseUrl/$ExeName.sha256" -OutFile $tempSha -UseBasicParsing

    $expected = ((Get-Content -LiteralPath $tempSha -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    $actual = (Get-FileHash -LiteralPath $tempExe -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expected -ne $actual) {
        throw "Checksum mismatch: expected $expected but got $actual."
    }
    Write-Host 'Checksum verified.'

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    $destination = Join-Path $InstallDir $ExeName
    Move-Item -LiteralPath $tempExe -Destination $destination -Force

    $addedToPath = Add-ToUserPath -Directory $InstallDir

    Write-Host ''
    Write-Host "Installed qcompact to $destination"
    if ($addedToPath) {
        Write-Host 'Added the install directory to your user PATH.'
    }
    Write-Host 'Open a NEW terminal, then run: qcompact --dry-run'
}
finally {
    Remove-Item -LiteralPath $tempExe, $tempSha -Force -ErrorAction SilentlyContinue
}
