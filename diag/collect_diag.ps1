# Сбор диагностики после запуска игры: одна команда, один файл отчёта.
#   powershell -ExecutionPolicy Bypass -File diag\collect_diag.ps1 -GameDir "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes" -Label T1
# Результат: diag\report_<Label>_<время>.txt — его и прислать (закоммитить).

param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Label = "run",
    [int]$Minutes = 30
)

$out = Join-Path $PSScriptRoot ("report_{0}_{1}.txt" -f $Label, (Get-Date -Format "yyyyMMdd_HHmmss"))
$since = (Get-Date).AddMinutes(-$Minutes)
$data = Join-Path $env:LOCALAPPDATA "GtaRadio"

function Section($t) { Add-Content $out "" -Encoding UTF8; Add-Content $out ("==== " + $t) -Encoding UTF8 }
function Line($t) { Add-Content $out $t -Encoding UTF8 }
function FileInfoLine($p) {
    if (Test-Path $p) { $f = Get-Item $p; Line ("{0}  {1:yyyy-MM-dd HH:mm:ss}  {2} bytes" -f $p, $f.LastWriteTime, $f.Length) }
    else { Line ("{0}  (no file)" -f $p) }
}
function Tail($p, $n) {
    FileInfoLine $p
    if (Test-Path $p) { Get-Content $p -Tail $n -Encoding UTF8 | ForEach-Object { Line $_ } }
}

Set-Content $out ("GTA Radio diag, label={0}, {1}" -f $Label, (Get-Date -Format "yyyy-MM-dd HH:mm:ss")) -Encoding UTF8

Section "processes"
Get-Process -Name GTA5_Enhanced, PlayGTAV, GtaRadio.Agent, GtaRadio -ErrorAction SilentlyContinue |
    ForEach-Object { Line ("{0} pid={1} start={2}" -f $_.ProcessName, $_.Id, $_.StartTime) }

Section "game root (asi/dll/ini/txt)"
Get-ChildItem $GameDir -File | Where-Object { $_.Extension -in ".asi", ".dll", ".ini", ".txt" } |
    ForEach-Object { Line ("{0,-40} {1:yyyy-MM-dd HH:mm}  {2}" -f $_.Name, $_.LastWriteTime, $_.Length) }

Section "scripts"
Get-ChildItem (Join-Path $GameDir "scripts") -Recurse -File -ErrorAction SilentlyContinue |
    ForEach-Object { Line ("{0,-60} {1:yyyy-MM-dd HH:mm}  {2}" -f $_.FullName.Substring($GameDir.Length), $_.LastWriteTime, $_.Length) }

Section "rpf"
FileInfoLine (Join-Path $GameDir "mods\update\update.rpf")
FileInfoLine (Join-Path $GameDir "update\update.rpf")

Section "ScriptHookV.log (tail 40)"
Tail (Join-Path $GameDir "ScriptHookV.log") 40
Section "ScriptHookVDotNet.log (tail 80)"
Tail (Join-Path $GameDir "ScriptHookVDotNet.log") 80
Section "probe.log"
Tail (Join-Path $data "probe.log") 40
Section "mod.log"
Tail (Join-Path $data "mod.log") 80
Section "agent.log"
Tail (Join-Path $data "agent.log") 80
Section "sessions.json (player volumes the agent must restore)"
Tail (Join-Path $data "sessions.json") 20

Section "Application event log, last $Minutes min"
try {
    Get-WinEvent -FilterHashtable @{ LogName = "Application"; StartTime = $since; Id = 1000, 1001, 1002, 1026 } -ErrorAction Stop |
        Where-Object { $_.Message -match "GTA5|ScriptHook|GtaRadio|clr|\.NET" } |
        ForEach-Object { Line ("--- {0:HH:mm:ss} id={1} {2}" -f $_.TimeCreated, $_.Id, $_.ProviderName); Line $_.Message }
} catch { Line "(no events)" }

Section "CrashDumps / WER"
Get-ChildItem (Join-Path $env:LOCALAPPDATA "CrashDumps") -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $since } |
    ForEach-Object { Line ("{0} {1:HH:mm:ss} {2}" -f $_.Name, $_.LastWriteTime, $_.Length) }
Get-ChildItem "$env:ProgramData\Microsoft\Windows\WER\ReportArchive", "$env:LOCALAPPDATA\Microsoft\Windows\WER\ReportArchive" -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "GTA5" -and $_.LastWriteTime -gt $since } |
    ForEach-Object { Line ("WER {0} {1:HH:mm:ss}" -f $_.Name, $_.LastWriteTime) }

Write-Host "[OK] report: $out"
