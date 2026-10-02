#Requires -Version 5.1
<#
.SYNOPSIS
    Reclaim unused disk space from WSL 2 virtual disks (ext4.vhdx).

.DESCRIPTION
    quick-compact finds the ext4.vhdx file behind each installed WSL 2 distribution,
    shuts WSL down, compacts the virtual disks with DiskPart, and reports how much
    space was recovered.

    It never deletes Linux files, never removes a distribution, and never resizes a
    disk. It only gives back blocks that are already free inside the VHDX.

    DiskPart compaction requires elevation, so the script relaunches itself with a
    UAC prompt unless -NoElevate or -DryRun is supplied.

.PARAMETER Distro
    One or more distribution names to compact. Defaults to every WSL 2 distro found.

.PARAMETER DryRun
    Report the detected virtual disks and their sizes, then exit without shutting
    WSL down or compacting anything. Does not request elevation.

.PARAMETER NoElevate
    Do not attempt to self-elevate. Usable when you already launched from an
    elevated shell.

.PARAMETER KeepLog
    Keep the transcript log instead of deleting it when the script finishes.

.EXAMPLE
    .\quick-compact.ps1
    Detect all WSL 2 disks, prompt for elevation, compact them, report reclaimed space.

.EXAMPLE
    .\quick-compact.ps1 -DryRun
    Show what would be compacted without changing anything.

.EXAMPLE
    .\quick-compact.ps1 -Distro Ubuntu -NoElevate
    Compact only the Ubuntu distribution from an already-elevated shell.

.NOTES
    Requires Windows 10/11 with WSL 2 and PowerShell 5.1 or later.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string[]]$Distro,
    [switch]$DryRun,
    [switch]$NoElevate,
    [switch]$KeepLog
)

$ErrorActionPreference = 'Stop'

function Format-Size {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][long]$Bytes
    )
    $units = 'B', 'KB', 'MB', 'GB', 'TB'
    $value = [double]$Bytes
    $index = 0
    while ([Math]::Abs($value) -ge 1024 -and $index -lt ($units.Count - 1)) {
        $value /= 1024
        $index++
    }
    '{0:N2} {1}' -f $value, $units[$index]
}

function Test-IsAdmin {
    [CmdletBinding()]
    param()
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-WslVhdxPaths {
    <#
        Resolves each installed WSL distribution to its ext4.vhdx from the Lxss
        registry hive. -RegistryRoot exists so tests can point at a scratch key.
    #>
    [CmdletBinding()]
    param(
        [string]$RegistryRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss'
    )
    if (-not (Test-Path -LiteralPath $RegistryRoot)) { return }

    Get-ChildItem -LiteralPath $RegistryRoot | ForEach-Object {
        $props = Get-ItemProperty -LiteralPath $_.PSPath
        $name = $props.PSObject.Properties['DistributionName']
        $base = $props.PSObject.Properties['BasePath']
        if ($null -eq $name -or $null -eq $base -or -not $base.Value) { return }

        $vhdx = Join-Path $base.Value 'ext4.vhdx'
        [pscustomobject]@{
            Distro   = $name.Value
            BasePath = $base.Value
            Vhdx     = $vhdx
            Exists   = (Test-Path -LiteralPath $vhdx -PathType Leaf)
        }
    }
}

function New-DiskpartScript {
    <#
        Builds the DiskPart script that compacts one VHDX. Kept separate so the
        quoting can be unit tested without touching a real disk.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$VhdxPath
    )
    @(
        "select vdisk file=`"$VhdxPath`""
        'attach vdisk readonly'
        'compact vdisk'
        'detach vdisk'
        'exit'
    ) -join "`r`n"
}

function Invoke-VhdCompact {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$VhdxPath
    )
    $scriptFile = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -LiteralPath $scriptFile -Value (New-DiskpartScript -VhdxPath $VhdxPath) -Encoding ASCII
        $output = & diskpart.exe /s $scriptFile 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "diskpart exited with code $LASTEXITCODE`r`n$output"
        }
        return $output
    }
    finally {
        Remove-Item -LiteralPath $scriptFile -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Main. Skipped when the file is dot-sourced so tests can load the functions.
# ---------------------------------------------------------------------------
if ($MyInvocation.InvocationName -ne '.') {

    $InvocationArgs = $PSBoundParameters
    $exitCode = 0
    $logPath = Join-Path $env:TEMP ('quick-compact-{0:yyyyMMdd-HHmmss}.log' -f (Get-Date))
    $transcriptStarted = $false

    try {
        try {
            Start-Transcript -Path $logPath -ErrorAction Stop | Out-Null
            $transcriptStarted = $true
        }
        catch {
            # A transcript is a convenience; carry on without it.
            Write-Verbose "Could not start transcript: $($_.Exception.Message)"
        }

        $targets = @(Get-WslVhdxPaths | Where-Object { $_.Exists })
        if ($Distro) {
            $targets = @($targets | Where-Object { $Distro -contains $_.Distro })
        }

        if ($targets.Count -eq 0) {
            Write-Warning 'No WSL 2 virtual disks found. Is WSL installed, and are the distros in their default location?'
            exit 0
        }

        Write-Host 'WSL 2 virtual disks detected:'
        foreach ($target in $targets) {
            try { $size = (Get-Item -LiteralPath $target.Vhdx).Length }
            catch { $size = 0 }
            Write-Host ('  {0,-24} {1,10}  {2}' -f $target.Distro, (Format-Size $size), $target.Vhdx)
        }

        if ($DryRun) {
            Write-Host "`nDry run: nothing was changed."
            exit 0
        }

        if (-not (Test-IsAdmin) -and -not $NoElevate -and -not $WhatIfPreference) {
            Write-Host "`nElevation is required to compact virtual disks. Relaunching..."
            $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
            foreach ($entry in $InvocationArgs.GetEnumerator()) {
                if ($entry.Value -is [switch]) {
                    if ($entry.Value.IsPresent) { $argList += "-$($entry.Key)" }
                }
                elseif ($entry.Value -is [array]) {
                    foreach ($item in $entry.Value) { $argList += @("-$($entry.Key)", "`"$item`"") }
                }
                else {
                    $argList += @("-$($entry.Key)", "`"$($entry.Value)`"")
                }
            }
            Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $argList -Verb RunAs
            exit 0
        }

        if (-not (Test-IsAdmin) -and $NoElevate) {
            Write-Warning 'Not elevated and -NoElevate was supplied; DiskPart compaction will likely fail.'
        }

        if (-not $PSCmdlet.ShouldProcess('all WSL distributions', 'shut down WSL and compact the virtual disks')) {
            exit 0
        }

        Write-Host "`nShutting down WSL..."
        & wsl.exe --shutdown 2>$null | Out-Null
        Start-Sleep -Seconds 2

        [long]$reclaimed = 0
        foreach ($target in $targets) {
            try {
                $before = (Get-Item -LiteralPath $target.Vhdx).Length
            }
            catch {
                Write-Warning "Skipping $($target.Distro): cannot read $($target.Vhdx)"
                continue
            }

            Write-Host "Compacting $($target.Distro)..."
            try {
                $null = Invoke-VhdCompact -VhdxPath $target.Vhdx
            }
            catch {
                Write-Error "Compaction failed for $($target.Distro): $($_.Exception.Message)" -ErrorAction Continue
                $exitCode = 1
                continue
            }

            $after = (Get-Item -LiteralPath $target.Vhdx).Length
            $delta = [long]$before - [long]$after
            $reclaimed += $delta
            Write-Host ('  {0}: {1} -> {2}  (reclaimed {3})' -f $target.Distro, (Format-Size $before), (Format-Size $after), (Format-Size $delta))
        }

        Write-Host "`nTotal reclaimed: $(Format-Size $reclaimed)"
        Write-Host 'Start your WSL distribution again to continue working.'
        exit $exitCode
    }
    finally {
        if ($transcriptStarted) {
            try { Stop-Transcript | Out-Null }
            catch { Write-Verbose "Could not stop transcript: $($_.Exception.Message)" }
        }
        if ($KeepLog -and $transcriptStarted) {
            Write-Host "Log saved: $logPath"
        }
        else {
            Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
        }
    }
}
