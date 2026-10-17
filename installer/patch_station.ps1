# Переименование станции RADIO_02_POP в "GTA Radio", метки текста трека для колеса, и откат.
#
# Правится только одна строка GXT2 (метка RADIO_02_POP = 0xB5DD6F19) в оверлее mods\update\update.rpf,
# в американском и русском global.gxt2. Остальные строки — перевод и радио-озвучка — остаются
# байт в байт. Оригинальные RPF игры не трогаются.
#
# Бэкап: <game>\GtaRadio\backup\<дата>\ — копия каждого global.gxt2 до правки и исходное значение метки
# (original_<lang>.txt, пишется только один раз, чтобы переустановка не запомнила "GTA Radio" как оригинал).
# Откат (-Uninstall) возвращает в ТЕКУЩИЙ update.rpf только исходное значение метки — если озвучку
# обновили после установки, её изменения сохраняются.
#
# Инструменты: gta-patch (CodeWalker-based, Gen9) и GtaRadio.Gxt2.exe (правка одной строки).
#
# [CHECK] Команды gta-patch extract/put взяты из docs\BUILD.md. Как именно put выбирает путь
#         внутри RPF, нужно проверить на ПК (см. docs\DEEPSEEK.md, шаг 3) и при необходимости
#         поправить функцию Put-Entry ниже. До проверки запускать только с -DryRun на копии.

param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Label = "RADIO_02_POP",
    [string]$Text = "GTA Radio",
    [switch]$Uninstall,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Join-Path $GameDir "GtaRadio"
$gtaPatch = Join-Path $root "tools\gta-patch\gta-patch.exe"
$gxt2 = Join-Path $root "tools\GtaRadio.Gxt2.exe"
$rpf = Join-Path $GameDir "mods\update\update.rpf"
$langs = @("american", "russian")
$backupRoot = Join-Path $root "backup"
$log = Join-Path $root "patch_station.log"

function Log($m) { $line = (Get-Date -Format "yyyy-MM-dd HH:mm:ss ") + $m; Add-Content -Path $log -Value $line -Encoding UTF8; Write-Host $line }

function Entry($lang) { "update.rpf\x64\patch\data\lang\${lang}_rel.rpf\global.gxt2" }

function EntryBase($lang) { "x64b.rpf\data\lang\${lang}_rel.rpf\global.gxt2" }

function Extract-Entry($lang, $outDir, $fromRpf = $rpf, $entryPath = $null) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    if (-not $entryPath) { $entryPath = Entry $lang }
    & $gtaPatch extract $GameDir $fromRpf $outDir $entryPath | Out-Null
    $file = Get-ChildItem -Path $outDir -Recurse | Where-Object { $_.Name -like "*global.gxt2" } | Select-Object -First 1
    if (-not $file) { throw "extract failed for $lang" }
    return $file.FullName
}

function Put-Entry($lang, $file) {
    # [CHECK] put <game> <rpf> <file...> — см. заголовок.
    & $gtaPatch put $GameDir $rpf $file | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "put failed for $lang (exit $LASTEXITCODE)" }
}

$WheelLabels = @("GTARADIO_ARTIST", "GTARADIO_TITLE")
$Placeholder = " " * 127

function Has-Label($file, $label) {
    & $gxt2 get $file $label *> $null
    return ($LASTEXITCODE -eq 0)
}

function Gxt2($argsList) {
    $out = & $gxt2 @argsList 2>&1
    if ($LASTEXITCODE -ne 0) { throw "gxt2 $($argsList -join ' '): $out" }
    return ($out | Out-String).Trim()
}

if (-not (Test-Path $gtaPatch)) { throw "gta-patch not found: $gtaPatch" }
if (-not (Test-Path $gxt2)) { throw "GtaRadio.Gxt2 not found: $gxt2" }
if (-not (Test-Path $rpf)) {
    if ($Uninstall) { Log "[OK] no mods\update\update.rpf, nothing to restore"; exit 0 }
    # Оверлея ещё нет: создаём его копией оригинала (оригинал не меняется).
    $orig = Join-Path $GameDir "update\update.rpf"
    if (-not (Test-Path $orig)) { throw "update\update.rpf not found" }
    New-Item -ItemType Directory -Force -Path (Split-Path $rpf) | Out-Null
    if (-not $DryRun) { Copy-Item $orig $rpf; Log "[OK] created mods\update\update.rpf from original" }
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$work = Join-Path $env:TEMP "GtaRadio_gxt2_$stamp"
$backup = Join-Path $backupRoot $stamp
New-Item -ItemType Directory -Force -Path $backup | Out-Null

foreach ($lang in $langs) {
    $file = Extract-Entry $lang (Join-Path $work $lang)
    Copy-Item $file (Join-Path $backup "global_$lang.gxt2")
    $current = Gxt2 @("get", $file, $Label)
    $origFile = Join-Path $backupRoot "original_$lang.txt"

    if ($Uninstall) {
        if (-not (Test-Path $origFile)) { Log "[ERROR] no saved original for $lang, skip"; continue }
        $value = [IO.File]::ReadAllText($origFile, [Text.Encoding]::UTF8)
    } else {
        if (-not (Test-Path $origFile)) {
            $original = $current
            if ($current -eq $Text) {
                # Станцию уже переименовали раньше (вручную или прошлой версией): оригинал берём из
                # нетронутого update\update.rpf игры, а не из оверлея.
                $vanilla = Extract-Entry $lang (Join-Path $work "vanilla_$lang") (Join-Path $GameDir "x64b.rpf") (EntryBase $lang)
                $original = Gxt2 @("get", $vanilla, $Label)
            }
            [IO.File]::WriteAllText($origFile, $original, [Text.Encoding]::UTF8)
            Log "[OK] $lang original saved: '$original'"
        }
        $value = $Text
    }

    # Две свои метки для текста трека в колесе радио (мод пишет туда артиста и название в эфире).
    # Плейсхолдер 127 пробелов: SHVDN SetLocalizedString пишет поверх строки той же длины.
    $changed = $false
    if ($current -ne $value) {
        Gxt2 @("set", $file, $Label, $value) | Out-Null
        if ((Gxt2 @("get", $file, $Label)) -ne $value) { throw "verify failed for $lang" }
        $changed = $true
    }
    foreach ($wl in $WheelLabels) {
        $has = Has-Label $file $wl
        if ($Uninstall -and $has) { Gxt2 @("remove", $file, $wl) | Out-Null; $changed = $true }
        elseif (-not $Uninstall -and -not $has) { Gxt2 @("add", $file, $wl, $Placeholder) | Out-Null; $changed = $true }
    }

    if (-not $changed) { Log "[OK] $lang already '$value', wheel labels ok"; continue }
    if ($DryRun) { Log "[DRY] $lang '$current' -> '$value', wheel labels updated (not written)"; continue }
    Put-Entry $lang $file

    # Проверка извлечением из RPF.
    $verify = Extract-Entry $lang (Join-Path $work "verify_$lang")
    $after = Gxt2 @("get", $verify, $Label)
    if ($after -ne $value) { throw "rpf verify failed for ${lang}: '$after'" }
    foreach ($wl in $WheelLabels) {
        if ((Has-Label $verify $wl) -eq [bool]$Uninstall) { throw "rpf verify failed for ${lang}: label $wl" }
    }
    Log "[OK] $lang '$current' -> '$value', wheel labels $(if ($Uninstall) { 'removed' } else { 'present' })"
}

Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
Log "[OK] done (backup: $backup)"
