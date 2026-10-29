@echo off
rem Publishes a new version for the built-in updaters of both apps:
rem   1. bump <Version> in src\TaskWidget\TaskWidget.csproj (e.g. 1.3.0 -> 1.4.0) and commit;
rem   2. run release.bat (optionally with release notes: release.bat "Що нового: ...").
rem It builds dist\TaskWidget.exe and dist\DayTasks.apk, pushes the commit and creates the GitHub
rem release vX.Y.Z with both files and their .sha256 - the apps find it within a few hours.
rem Needs: git, GitHub CLI (gh auth login), and everything build.bat needs.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 %*
set code=%errorlevel%
pause
exit /b %code%
