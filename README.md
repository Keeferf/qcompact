# quick-compact

Safely reclaim unused disk space from WSL 2 virtual disks.

WSL 2 stores each distribution in a dynamically expanding `ext4.vhdx`. Files you
delete inside Linux free space *inside* the disk, but the VHDX file on Windows
does not shrink on its own. `quick-compact` finds those VHDX files, shuts WSL
down, compacts them with DiskPart, and tells you how much space you got back.

## What it does

1. Detects installed WSL 2 virtual disks from the `Lxss` registry hive.
2. Shows each distribution and its current Windows-side disk usage.
3. Shuts WSL down (`wsl --shutdown`).
4. Compacts each `ext4.vhdx` with DiskPart.
5. Reports the space recovered.

## What it does *not* do

- Delete Linux files.
- Remove WSL distributions.
- Modify your files or uninstall software.
- Resize a disk or move a distribution to another drive.

It only returns blocks that are already free inside the virtual disk.

## Requirements

- Windows 10 or 11 with WSL 2.
- PowerShell 5.1 or later (Windows PowerShell or PowerShell 7+).
- Administrator rights. DiskPart compaction needs elevation, so the script
  prompts for UAC automatically.

## Usage

Open PowerShell in this folder and run:

```powershell
.\quick-compact.ps1
```

Or launch it from Explorer / cmd with:

```bat
quick-compact.bat
```

> `quick-compact.bat` is only a launcher; it just calls the PowerShell script.

### Preview first

```powershell
.\quick-compact.ps1 -DryRun
```

`-DryRun` lists the detected disks and their sizes, then exits without shutting
WSL down or changing anything. It does not request elevation.

### Compact one distribution

```powershell
.\quick-compact.ps1 -Distro Ubuntu
```

### Run from an already-elevated shell

```powershell
.\quick-compact.ps1 -NoElevate
```

### Keep the transcript for troubleshooting

```powershell
.\quick-compact.ps1 -KeepLog
```

### Standard `-WhatIf` support

```powershell
.\quick-compact.ps1 -WhatIf
```

## Parameters

| Parameter    | Description                                                              |
| ------------ | ------------------------------------------------------------------------ |
| `-Distro`    | One or more distribution names to compact. Defaults to all detected.     |
| `-DryRun`    | Report only; no shutdown, no compaction, no elevation.                   |
| `-NoElevate` | Do not self-elevate (for use from an elevated shell).                    |
| `-KeepLog`   | Keep the transcript log instead of deleting it.                          |
| `-WhatIf`    | Standard PowerShell switch; shows what would happen.                     |

## Notes and safety

- **WSL is shut down.** Running distributions and their processes are terminated.
  Save your work first.
- Compaction is safe but can take a while on large disks.
- The first size shown is the VHDX file size on Windows; after compaction the
  file is smaller by the amount of free space that was returned.
- If compaction fails for one distribution, the others are still attempted and
  the script exits with a non-zero code for automation.

## Development

Tests use [Pester](https://pester.dev/) 5+ and run on Windows:

```powershell
Invoke-Pester ./tests
```

CI (`validate.yml`) runs PSScriptAnalyzer and Pester on `windows-latest`.

## License

MIT — see [LICENSE](LICENSE).
