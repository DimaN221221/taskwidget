# Build both apps and publish them as GitHub release v<Version> (see release.bat).
param([string]$Notes = "")
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot)

function Fail($text) { Write-Host "`n$text" -ForegroundColor Red; exit 1 }

[xml]$proj = Get-Content src\TaskWidget\TaskWidget.csproj -Encoding UTF8
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version.Trim()
$tag = "v$version"
Write-Host "=== Release $tag ===" -ForegroundColor Cyan

if (git status --porcelain) { Fail "There are uncommitted changes. Commit them first (the release is built from the commit)." }
gh release view $tag *> $null
if ($LASTEXITCODE -eq 0) { Fail "Release $tag already exists. Bump <Version> in src\TaskWidget\TaskWidget.csproj." }

cmd /c "`"$PWD\build.bat`" nopause"
if ($LASTEXITCODE -ne 0) { Fail "Build failed." }
$apk = Get-Item dist\DayTasks.apk
if ($apk.LastWriteTime -lt (Get-Date).AddMinutes(-30)) { Fail "dist\DayTasks.apk was not rebuilt (is JDK 17 installed?)." }

$files = @("dist\TaskWidget.exe", "dist\DayTasks.apk")
foreach ($f in $files) {
    $hash = (Get-FileHash $f -Algorithm SHA256).Hash.ToLower()
    [IO.File]::WriteAllText((Resolve-Path $f).Path + ".sha256", "$hash  $(Split-Path $f -Leaf)`n")
}

git push origin HEAD
if ($LASTEXITCODE -ne 0) { Fail "git push failed." }

$assets = $files + ($files | ForEach-Object { "$_.sha256" })
if ($Notes) { gh release create $tag @assets --title "TaskWidget $version" --notes $Notes --target (git rev-parse HEAD) }
else { gh release create $tag @assets --title "TaskWidget $version" --generate-notes --target (git rev-parse HEAD) }
if ($LASTEXITCODE -ne 0) { Fail "gh release create failed." }

git fetch --tags --quiet
Write-Host "`nDone: $tag is published. The apps will offer it on their next update check." -ForegroundColor Green
