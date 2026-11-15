# Run from PowerShell after creating an EMPTY repository named dark-together on GitHub.
# Usage: powershell -ExecutionPolicy Bypass -File .\upload-to-github.ps1
$ErrorActionPreference = 'Stop'
$repo = 'https://github.com/Thebadboy209/dark-together.git'

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is not installed. Install Git for Windows from https://git-scm.com/downloads/win'
}
Set-Location $PSScriptRoot

function Run-Git {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$GitArgs)
    & git @GitArgs
    if ($LASTEXITCODE -ne 0) { throw "Git failed: git $($GitArgs -join ' ')" }
}

if (-not (Test-Path '.git')) { Run-Git init }
Run-Git add -A
& git diff --cached --quiet
if ($LASTEXITCODE -eq 1) {
    Run-Git commit -m 'Initial Unity 2D co-op horror starter'
} elseif ($LASTEXITCODE -ne 0) {
    throw 'Unable to check staged changes.'
}
Run-Git branch -M main
$origin = & git remote get-url origin 2>$null
if ($LASTEXITCODE -ne 0) {
    Run-Git remote add origin $repo
} elseif ($origin.Trim() -ne $repo) {
    throw "Existing origin remote is '$origin', expected '$repo'. Check before pushing."
}
Run-Git push -u origin main
Write-Host "Upload complete: https://github.com/Thebadboy209/dark-together" -ForegroundColor Green
