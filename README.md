# qcompact

Safely reclaim unused disk space from WSL 2 virtual disks.

WSL 2 stores each distribution in a dynamically expanding `ext4.vhdx`. Files you
delete inside Linux free space *inside* the disk, but the VHDX file on Windows
does not shrink on its own. `qcompact` finds those VHDX files, shuts WSL down,
compacts them with DiskPart, and tells you how much space you got back.

## What it does

1. Detects installed WSL 2 virtual disks from the `Lxss` registry hive.
2. Shows each distribution and its current Windows-side disk usage.
3. Runs `fstrim` inside each distribution so freed blocks are marked free.
4. Shuts WSL down (`wsl --shutdown`).
5. Compacts each `ext4.vhdx` with DiskPart.
6. Reports the space recovered.

## What it does *not* do

- Delete Linux files.
- Remove WSL distributions.
- Modify your files or uninstall software.
- Resize a disk or move a distribution to another drive.

It only returns blocks that are already free inside the virtual disk.

## Requirements

- Windows 10 or 11 with WSL 2.
- Administrator rights. DiskPart compaction needs elevation, so `qcompact`
  self-elevates with a UAC prompt by default. Pass `--no-elevate` to disable
  and require an already-elevated terminal instead.

## Install

There are two ways to install: the PowerShell one-liner, or a direct download
from the releases page.

### PowerShell one-liner

```powershell
iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing | iex
```

Installs the latest release to `%LOCALAPPDATA%\Programs\qcompact`, verifies the
SHA256 checksum, and adds it to your user `PATH`. Open a **new** terminal
afterward.

Pin a specific version, or uninstall:

```powershell
# Pin a version
& ([scriptblock]::Create((iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing).Content)) -Version 1.1.2

# Uninstall
& ([scriptblock]::Create((iwr https://raw.githubusercontent.com/Keeferf/qcompact/main/install.ps1 -UseBasicParsing).Content)) -Uninstall
```

### Direct download

Download `qcompact.exe` from the
[latest release](https://github.com/Keeferf/qcompact/releases/latest) and run it.
Every release also publishes a `qcompact.exe.sha256` checksum.

After installing, open a **new** terminal so the updated `PATH` is picked up.

### Updating

`qcompact` can update itself:

```powershell
qcompact self-update
```

Downloads the latest release from GitHub, verifies its SHA256 checksum, swaps
it into place, and relaunches with the new version. A normal run (not `--json`
or `--dry-run`) also prints a one-line notice when a newer release exists.

## Usage

```
qcompact [options]
```

| Option | Description |
| ------ | ----------- |
| `-d`, `--distro <name>` | One or more distributions to compact. Repeatable. Defaults to all detected. |
| `--dry-run` | List detected disks and sizes, then exit. Changes nothing; no elevation. |
| `--no-elevate` | Do not relaunch elevated. Fails if not already running as administrator. |
| `-y`, `--yes` | Do not prompt for confirmation. Required when not running interactively. |
| `--json` | Emit a machine-readable JSON result instead of human-readable output. Never auto-elevates. |
| `--keep-log` | Keep the transcript log file instead of deleting it. |
| `--verbose` | Verbose output. |
| `-v`, `--version` | Show version information. |
| `-h`, `--help` | Show help. |

> `--json` is meant for automation. Because self-elevation opens a separate
> console, JSON mode never auto-elevates; run it from an already-elevated shell.

### Examples

```powershell
# Preview without changing anything
qcompact --dry-run

# Compact everything (prompts for UAC, then for confirmation)
qcompact

# Compact one distribution
qcompact --distro Ubuntu

# Non-interactive: skip the prompt
qcompact --yes

# Update to the latest release from GitHub
qcompact self-update

# Already running as administrator; do not attempt a UAC relaunch
qcompact --no-elevate
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
