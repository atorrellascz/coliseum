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
# Through run.ps1: a plain `bash` would be WSL on machines that have it (see scripts/run.ps1).
& (Join-Path $PSScriptRoot "run.ps1") smoke.sh
exit $LASTEXITCODE
