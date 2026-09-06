# Run one of the scripts/*.sh files with Git Bash from PowerShell.
#   .\scripts\run.ps1 k3d-up.sh
#   .\scripts\run.ps1 chaos-worker.sh            (environment: $env:MODE="k8s"; $env:KUBE_CONTEXT="k3d-coliseum")
# Why: on a machine with WSL, a plain `bash` in PowerShell is C:\Windows\System32\bash.exe (the Linux distro),
# which has neither k3d, helm nor kubectl. The scripts are written for Git Bash (ships with Git for Windows).
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Script,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$ScriptArgs
)
$candidates = @("$env:ProgramFiles\Git\bin\bash.exe", "${env:ProgramFiles(x86)}\Git\bin\bash.exe", "$env:LocalAppData\Programs\Git\bin\bash.exe")
$git = Get-Command git.exe -ErrorAction SilentlyContinue
if ($git) { $candidates += (Join-Path (Split-Path (Split-Path $git.Source -Parent) -Parent) "bin\bash.exe") }
$bash = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $bash) { throw "Git Bash not found (install Git for Windows: winget install Git.Git)." }
$path = Join-Path (Split-Path $PSScriptRoot -Parent) "scripts\$Script"
if (-not (Test-Path $path)) { throw "no such script: $path" }
& $bash $path @ScriptArgs
exit $LASTEXITCODE
