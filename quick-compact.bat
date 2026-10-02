@echo off
rem Convenience launcher for quick-compact.ps1. All arguments are passed through.
setlocal
set "PS1=%~dp0quick-compact.ps1"

where pwsh >nul 2>&1
if %ERRORLEVEL%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
)
exit /b %ERRORLEVEL%
