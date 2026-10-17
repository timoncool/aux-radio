param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$root = Join-Path $GameDir "GtaRadio"
$logo = Join-Path $root "logo"
$cache = Join-Path $logo "ng-cache"
$exe = Join-Path $logo "GtaRadio.Logo.exe"
$log = Join-Path $root "logo_patch.log"
$rpf = Join-Path (Join-Path $GameDir "mods\update") "update.rpf"

function Log($m) {
    $line = (Get-Date -Format "yyyy-MM-dd HH:mm:ss ") + $m
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $line
}

if (-not (Test-Path $exe)) { throw "GtaRadio.Logo.exe not found: $exe" }
if (-not (Test-Path $rpf)) { throw "update.rpf not found: $rpf" }

$file = Join-Path $logo $(if ($Uninstall) { "hud_original.ytd" } else { "hud_logo.ytd" })
if (-not (Test-Path $file)) { throw "logo resource not found: $file" }

$keyFile = Join-Path $cache "gtav_aes_key.b64"
if (-not (Test-Path $keyFile)) { throw "key not found: $keyFile" }
$key = (Get-Content $keyFile -Raw).Trim()

$out = & $exe put --game $GameDir --rpf "mods/update/update.rpf" `
    --entry "x64/data/cdimages/scaleform_generic.rpf/hud.ytd" `
    --key $key --tables $cache --file $file 2>&1
$code = $LASTEXITCODE
foreach ($l in $out) { Log $l }
if ($code -ne 0) { throw "logo put failed (exit $code)" }

if ($Uninstall) { Log "[OK] logo restored" } else { Log "[OK] logo installed" }
