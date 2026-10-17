# Сборка GTA Radio: мод, агент, утилита GXT2, payload установщика и сам установщик.
#
#   .\build.ps1 -GtaPatchDir <папка gta-patch> -ShvDir <папка с ScriptHookV/SHVDN> [-Iscc <ISCC.exe>] [-Deploy <папка игры>]
#
# -GtaPatchDir  папка с gta-patch.exe (из проекта дубляжа: music-studios\game-dub\games\gta5\tools\gta-patch\...)
# -ShvDir       ScriptHookV.dll, dinput8.dll, ScriptHookVDotNet.asi, ScriptHookVDotNet3.dll, ScriptHookVDotNet.ini
#               (официальные сборки dev-c.com и Chiheb-Bacha/ScriptHookVDotNetEnhanced; в репозиторий не входят)
# -Deploy       скопировать мод и агент прямо в игру без установщика (для быстрой проверки)

param(
    [Parameter(Mandatory = $true)][string]$GtaPatchDir,
    [Parameter(Mandatory = $true)][string]$ShvDir,
    [string]$Iscc = "F:\AI\InnoSetup\ISCC.exe",
    [string]$Deploy
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$payload = Join-Path $root "installer\payload"

# ScriptHookVDotNet3.dll нужен для компиляции мода (ссылка, в сборку не копируется).
$vendor = Join-Path $root "vendor\shvdn"
New-Item -ItemType Directory -Force -Path $vendor | Out-Null
Copy-Item (Join-Path $ShvDir "ScriptHookVDotNet3.dll") $vendor -Force

Write-Host "[1/4] mod"
dotnet build (Join-Path $root "src\GtaRadio.Mod\GtaRadio.Mod.csproj") -c Release
if ($LASTEXITCODE -ne 0) { throw "mod build failed" }

Write-Host "[2/4] agent"
dotnet publish (Join-Path $root "src\GtaRadio.Agent\GtaRadio.Agent.csproj") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root "out\agent")
if ($LASTEXITCODE -ne 0) { throw "agent publish failed" }

Write-Host "[3/4] gxt2 tool"
dotnet publish (Join-Path $root "src\GtaRadio.Gxt2\GtaRadio.Gxt2.csproj") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o (Join-Path $root "out\gxt2")
if ($LASTEXITCODE -ne 0) { throw "gxt2 publish failed" }

Write-Host "[4/4] payload"
Remove-Item -Recurse -Force $payload -ErrorAction SilentlyContinue
$scripts = Join-Path $payload "scripts"
$agentDir = Join-Path $payload "GtaRadio"
$tools = Join-Path $agentDir "tools"
New-Item -ItemType Directory -Force -Path $scripts, $tools, (Join-Path $payload "shv") | Out-Null
$modOut = Join-Path $root "src\GtaRadio.Mod\bin\Release"
foreach ($f in "GtaRadio.Mod.dll", "NAudio.Core.dll", "NAudio.Wasapi.dll") { Copy-Item (Join-Path $modOut $f) $scripts }
Copy-Item (Join-Path $root "src\GtaRadio.Mod\GtaRadio.Mod.ini") $scripts
Copy-Item (Join-Path $root "out\agent\GtaRadio.Agent.exe") $agentDir
Copy-Item (Join-Path $root "out\gxt2\GtaRadio.Gxt2.exe") $tools
Copy-Item -Recurse (Join-Path $GtaPatchDir "*") (Join-Path $tools "gta-patch")
foreach ($f in "ScriptHookV.dll", "dinput8.dll", "ScriptHookVDotNet.asi", "ScriptHookVDotNet3.dll", "ScriptHookVDotNet.ini") {
    Copy-Item (Join-Path $ShvDir $f) (Join-Path $payload "shv")
}

if ($Deploy) {
    Write-Host "deploy -> $Deploy"
    # Старый мод 1.x (GtaRadioMod.dll) конфликтует с новым: убираем в бэкап, не удаляем.
    $old = Join-Path $Deploy "scripts\GtaRadioMod.dll"
    if (Test-Path $old) {
        $bak = Join-Path $Deploy "GtaRadio\backup\legacy"
        New-Item -ItemType Directory -Force -Path $bak | Out-Null
        Move-Item $old (Join-Path $bak "GtaRadioMod.dll") -Force
        Write-Host "legacy GtaRadioMod.dll -> $bak"
    }
    Copy-Item (Join-Path $scripts "*") (Join-Path $Deploy "scripts") -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $Deploy "GtaRadio\tools") | Out-Null
    Copy-Item (Join-Path $agentDir "GtaRadio.Agent.exe") (Join-Path $Deploy "GtaRadio") -Force
    Copy-Item -Recurse -Force (Join-Path $tools "*") (Join-Path $Deploy "GtaRadio\tools")
    Copy-Item (Join-Path $root "installer\patch_station.ps1") (Join-Path $Deploy "GtaRadio\tools") -Force
}

if (Test-Path $Iscc) {
    & $Iscc (Join-Path $root "installer\gta_radio.iss")
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }
    Write-Host "[OK] installer: installer\output\GtaRadio_Setup_2.0.exe"
} else {
    Write-Host "ISCC not found ($Iscc) — payload ready, installer not built"
}
