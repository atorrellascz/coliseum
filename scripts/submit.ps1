# Build the submission package: a git bundle of the whole repository plus the project guide as PDF.
#   .\scripts\submit.ps1                          # writes to ..\submission (outside the repo)
#   .\scripts\submit.ps1 -OutDir C:\tmp\coliseum-submission
# The bundle carries every ref (main + tags) and is verified with `git bundle verify`. Ignored folders
# (_referencia, bin, obj, .terraform) are never part of a bundle: it packs commits, not the working tree.
# PDF: markdown -> HTML (python-markdown) -> PDF (Chrome or Edge headless). Both are already on a dev box.
param(
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "..\submission"),
    [string]$Surname = "Torrellas",
    [string]$FirstName = "Atahualpa"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

if (git status --porcelain) { Write-Warning "working tree is not clean: uncommitted changes are NOT in the bundle" }

# 1. bundle
$bundle = Join-Path $OutDir "${Surname}_${FirstName}.bundle"
git bundle create $bundle --all
if ($LASTEXITCODE -ne 0) { throw "git bundle create failed" }
git bundle verify $bundle
if ($LASTEXITCODE -ne 0) { throw "git bundle verify failed" }

# 2. project guide -> PDF
$md = Join-Path $repo "docs\project-guide.md"
$html = Join-Path $OutDir "${Surname}_${FirstName}_project-guide.html"
$pdf = Join-Path $OutDir "${Surname}_${FirstName}_project-guide.pdf"
$css = @'
body{font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:11pt;line-height:1.45;max-width:19cm;margin:0 auto;color:#111}
h1{font-size:20pt;border-bottom:2px solid #333;padding-bottom:4px} h2{font-size:15pt;margin-top:22pt;border-bottom:1px solid #999}
h3{font-size:12pt;margin-top:14pt} code{font-family:Consolas,monospace;font-size:9.5pt;background:#f3f3f3;padding:0 3px}
pre{background:#f3f3f3;padding:8px;font-size:9pt;white-space:pre-wrap} img{max-width:100%;border:1px solid #ccc;margin-top:6px} p>em{font-size:9.5pt;color:#444} table{border-collapse:collapse;width:100%;font-size:9.5pt;margin:8px 0}
th,td{border:1px solid #bbb;padding:4px 6px;vertical-align:top;text-align:left} th{background:#eee} tr{page-break-inside:avoid}
blockquote{border-left:3px solid #999;margin:0;padding:2px 10px;color:#444}
'@
$py = @"
import markdown, io
import os, re
src = io.open(r'$md', encoding='utf-8').read()
imgdir = os.path.join(os.path.dirname(r'$md'), 'images')
for name in re.findall(r'\]\(images/([^)]+)\)', src):
    if not os.path.exists(os.path.join(imgdir, name)): print('WARNING: missing screenshot docs/images/' + name)
src = src.replace('](images/', '](file:///' + imgdir.replace(os.sep, '/') + '/')
body = markdown.markdown(src, extensions=['tables', 'fenced_code', 'toc'])
html = '<!doctype html><html><head><meta charset="utf-8"><title>Coliseum project guide</title><style>' + r'''$css''' + '</style></head><body>' + body + '</body></html>'
io.open(r'$html', 'w', encoding='utf-8', newline='\n').write(html)
"@
python -c $py
if ($LASTEXITCODE -ne 0) { throw "markdown -> html failed (pip install markdown)" }

$browser = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw "no Chrome/Edge found for the PDF; open the HTML and print it to PDF" }
if (Test-Path $pdf) { Remove-Item $pdf }
# A separate user-data-dir keeps the print job away from an already running browser. On Windows the launcher
# process returns before the PDF is written, so poll for the file instead of trusting the exit code.
$profile = Join-Path $env:TEMP "coliseum-pdf-profile"
& $browser --headless=new --no-first-run --disable-gpu --no-pdf-header-footer --user-data-dir="$profile" --print-to-pdf="$pdf" "file:///$($html -replace '\\','/')" 2>$null | Out-Null
$deadline = (Get-Date).AddSeconds(90); $size = -1
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    if (Test-Path $pdf) { $now = (Get-Item $pdf).Length; if ($now -gt 0 -and $now -eq $size) { break }; $size = $now }
}
if (-not (Test-Path $pdf)) { throw "PDF was not produced (open $html in a browser and print it to PDF)" }
Remove-Item $html
Remove-Item -Recurse -Force $profile -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Submission files in $OutDir"
Get-ChildItem $OutDir | Format-Table Name, @{n='KB';e={[math]::Round($_.Length/1KB)}} -AutoSize
Write-Host "Check the bundle:  git clone `"$bundle`" coliseum-check"
