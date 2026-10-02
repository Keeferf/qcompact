# qcompact

Safely reclaim unused disk space from WSL 2 virtual disks.

WSL 2 stores each distribution in a dynamically expanding `ext4.vhdx`. Files you
delete inside Linux free space *inside* the disk, but the VHDX file on Windows
does not shrink on its own. `qcompact` finds those VHDX files, shuts WSL down,
compacts them with DiskPart, and tells you how much space you got back.

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
- Administrator rights. DiskPart compaction needs elevation, so run from an
  elevated terminal or pass `--elevate` to trigger a UAC prompt.

## Install

Once installed, `qcompact` is on your `PATH` and can be run from any terminal.

### PowerShell one-liner (no package manager required)

```powershell
iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing | iex
```

Installs the latest release to `%LOCALAPPDATA%\Programs\qcompact`, verifies the
SHA256 checksum, and adds it to your user `PATH`. Open a **new** terminal
afterward.

Pin a specific version, or uninstall:

```powershell
# Version 1.0.0
& ([scriptblock]::Create((iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing).Content)) -Version 1.0.0

# Uninstall
& ([scriptblock]::Create((iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing).Content)) -Uninstall
```

### Scoop

```powershell
scoop install https://raw.githubusercontent.com/Keeferf/qcompact/main/qcompact.json
```

### winget

```powershell
winget install Keeferf.qcompact
```

> The winget package is submitted to the community repository after the first
> release. Until then, use the one-liner, Scoop, or a direct download.

### Direct download

Download `qcompact.exe` from the
[latest release](https://github.com/Keeferf/qcompact/releases/latest) and run it.

After installing, open a **new** terminal so the updated `PATH` is picked up.

## Usage

```
qcompact [options]
```

| Option | Description |
| ------ | ----------- |
| `-d`, `--distro <name>` | One or more distributions to compact. Repeatable. Defaults to all detected. |
| `--dry-run` | List detected disks and sizes, then exit. Changes nothing; no elevation. |
| `--elevate` | Relaunch elevated (UAC) if not already running as administrator. |
| `-y`, `--yes` | Do not prompt for confirmation. Required when not running interactively. |
| `--json` | Emit a machine-readable JSON result instead of human-readable output. |
| `--keep-log` | Keep the transcript log file instead of deleting it. |
| `--verbose` | Verbose output. |
| `-v`, `--version` | Show version information. |
| `-h`, `--help` | Show help. |

### Examples

```powershell
# Preview without changing anything
qcompact --dry-run

# Compact everything (prompts for confirmation)
qcompact

# Compact one distribution
qcompact --distro Ubuntu

# Non-interactive: skip the prompt
qcompact --yes

# Run from a non-elevated shell, prompting for UAC
qcompact --elevate
```

### Exit codes

| Code | Meaning |
| ---- | ------- |
| `0` | Success. |
| `1` | One or more compactions failed. |
| `2` | Usage, permission, or confirmation error. |

## Notes and safety

- **WSL is shut down.** Running distributions and their processes are
  terminated. Save your work first.
- Compaction is safe but can take a while on large disks.
- The first size shown is the VHDX file size on Windows; after compaction the
  file is smaller by the amount of free space that was returned.
- If compaction fails for one distribution, the others are still attempted and
  the program exits with a non-zero code for automation.

## Development

Requires the .NET SDK (10.0 or later).

```powershell
dotnet build -c Release
dotnet test -c Release
```

Build a standalone single-file executable:

```powershell
dotnet publish src/QCompact/QCompact.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o publish
```

CI (`validate.yml`) builds and tests on `windows-latest`. Pushing a `v*` tag
builds `qcompact.exe` and attaches it to a GitHub Release (`release.yml`).

## License

MIT — see [LICENSE](LICENSE).
