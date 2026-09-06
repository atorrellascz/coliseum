# PowerShell wrapper for scripts/smoke.sh (which needs bash: Git Bash ships with Git for Windows).
#   .\scripts\smoke.ps1                                  # defaults: http://localhost:8080, dev-service-key
#   .\scripts\smoke.ps1 -ApiUrl http://localhost:5080    # e.g. a host started with dotnet run
param(
    [string]$ApiUrl = "http://localhost:8080",
    [string]$ApiKey = "dev-service-key",
    [int]$TimeoutSeconds = 30
)
$env:API_URL = $ApiUrl
$env:API_KEY = $ApiKey
$env:TIMEOUT_SECONDS = "$TimeoutSeconds"
$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) { throw "bash not found. Install Git for Windows (Git Bash) or run scripts/smoke.sh from WSL." }
& bash (Join-Path $PSScriptRoot "smoke.sh")
exit $LASTEXITCODE
