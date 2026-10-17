; Установщик AUX Radio для GTA V Enhanced.
; Стиль — как у установщика нейроозвучки (тёмный мастер, арт, проверка папки игры).
; Сборка: build.ps1 собирает payload\ и вызывает ISCC.exe (см. docs\DEEPSEEK.md).
;
; Что ставится:
;   scripts\GtaRadio.Mod.dll + NAudio.Core.dll + NAudio.Wasapi.dll + GtaRadio.Mod.ini  (мод)
;   GtaRadio\GtaRadio.Agent.exe                                                      (агент в трее)
;   GtaRadio\tools\gta-patch\*, GtaRadio\tools\GtaRadio.Gxt2.exe, patch_station.ps1     (имя станции)
;   ScriptHookV / SHVDN Enhanced — только если их ещё нет в папке игры.
; Совместимость с переводом и радио-озвучкой: в mods\update\update.rpf меняется одна строка
; RADIO_02_POP; при удалении в текущий файл возвращается только она. Оригинальные RPF не трогаются.

#define AppVer "2.0"
#define AppGuid "{A7F3D2C1-6B84-4E21-9C55-2F8D1A7B3C90}"

[Setup]
AppId={{#AppGuid}
AppName=AUX Radio
AppVersion={#AppVer}
AppVerName=AUX Radio {#AppVer} — Яндекс Музыка, Spotify и любой плеер вместо Non-Stop-Pop
AppPublisher=AUX Radio
DefaultDirName={code:DefaultGameDir}
DirExistsWarning=no
AppendDefaultDirName=no
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=output
OutputBaseFilename=AUX_Radio_Setup_{#AppVer}
WizardStyle=modern
WizardImageFile=art\wizard_radio.png
WizardSmallImageFile=art\small_radio.png
UninstallFilesDir={app}\GtaRadio\uninstall
UninstallDisplayName=AUX Radio
UninstallDisplayIcon={app}\GtaRadio\GtaRadio.Agent.exe
SetupIconFile=..\src\GtaRadio.Agent\app.ico
SetupLogging=yes

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ru.NoGame=В этой папке нет GTA5_Enhanced.exe. Укажите папку GTA V Enhanced.
en.NoGame=This folder has no GTA5_Enhanced.exe. Choose the GTA V Enhanced folder.
ru.GameRunningUnins=Игра запущена. Закройте GTA V перед удалением AUX Radio.
en.GameRunningUnins=The game is running. Close GTA V before uninstalling AUX Radio.
ru.StationFail=Не удалось переименовать станцию (код %1). Мод работает, но в колесе останется Non-Stop-Pop. Подробности: GtaRadio\patch_station.log
en.StationFail=Could not rename the station (code %1). The mod works, but the wheel will keep Non-Stop-Pop. Details: GtaRadio\patch_station.log
ru.LogoFail=Не удалось поставить логотип станции (код %1). Мод работает, логотип останется стандартным. Подробности: GtaRadio\logo_patch.log
en.LogoFail=Could not install the station logo (code %1). The mod works, the logo stays default. Details: GtaRadio\logo_patch.log
ru.CloseGame=Игра запущена. Закройте GTA V и нажмите «Назад», затем снова «Установить».
en.CloseGame=The game is running. Close GTA V, press Back, then Install again.
ru.StationStatus=Название станции: AUX Radio (с резервной копией)...
en.StationStatus=Station name: AUX Radio (with a backup)...
ru.LogoStatus=Логотип станции AUX Radio в колесе (с откатом)...
en.LogoStatus=Station logo in the radio wheel (with a rollback)...

[Messages]
ru.SetupWindowTitle=Установка — AUX Radio
en.SetupWindowTitle=Setup — AUX Radio
ru.WelcomeLabel1=AUX Radio
en.WelcomeLabel1=AUX Radio
ru.WelcomeLabel2=Радиостанция Non-Stop-Pop превращается в AUX Radio: в машине играет то, что звучит у тебя в Яндекс Музыке, Spotify или любом другом плеере.%n%nГромкость следует игре: ползунок музыки, пауза, катсцены, разговоры. Вышел из машины или переключил станцию — плеер на паузе.%n%nПеревод и радио-озвучка не затрагиваются: меняется только название одной станции, с резервной копией и откатом при удалении.
en.WelcomeLabel2=Non-Stop-Pop becomes AUX Radio: the car plays whatever is playing in your Spotify, Yandex Music or any other player.%n%nVolume follows the game: music slider, pause, cutscenes, conversations. Leave the car or switch the station — the player pauses.%n%nTranslations and radio voice-over are untouched: only one station name changes, with a backup and a rollback on uninstall.
ru.SelectDirLabel3=Укажите папку GTA V Enhanced (в ней лежит GTA5_Enhanced.exe).
en.SelectDirLabel3=Choose the GTA V Enhanced folder (it contains GTA5_Enhanced.exe).
ru.ReadyLabel1=Будут установлены мод, агент и название станции. Перед изменениями создаются резервные копии.
en.ReadyLabel1=The mod, the agent and the station name will be installed. Backups are made before changes.
ru.FinishedHeadingLabel=Готово
en.FinishedHeadingLabel=Done
ru.FinishedLabel=AUX Radio установлен.%n%nВключи музыку в плеере, запусти игру, сядь в машину и выбери AUX Radio. Агент запускается вместе с игрой и закрывается после неё.%n%nВ эфире: F7 — пауза, F8 — следующий трек, F6 — предыдущий.
en.FinishedLabel=AUX Radio is installed.%n%nStart your player, launch the game, get in a car and pick AUX Radio. The agent starts with the game and closes after it.%n%nOn air: F7 — pause, F8 — next track, F6 — previous.
ru.TypesFull=Полная установка
en.TypesFull=Full installation
ru.TypesCustom=Выборочная
en.TypesCustom=Custom
ru.CompMod=AUX Radio (мод и агент)
en.CompMod=AUX Radio (mod and agent)
ru.CompRename=Название станции «AUX Radio» в колесе радио
en.CompRename=Station name "AUX Radio" in the radio wheel
ru.CompShv=Script Hook V + ScriptHookVDotNet Enhanced (только если их ещё нет)
en.CompShv=Script Hook V + ScriptHookVDotNet Enhanced (only if missing)

[Types]
Name: "full"; Description: "Полная установка"
Name: "custom"; Description: "Выборочная"; Flags: iscustom

[Components]
Name: "mod"; Description: "AUX Radio (мод и агент)"; Types: full custom; Flags: fixed
Name: "rename"; Description: "Название станции «AUX Radio» в колесе радио"; Types: full custom
Name: "shv"; Description: "Script Hook V + ScriptHookVDotNet Enhanced (только если их ещё нет)"; Types: full custom

[Files]
Source: "payload\scripts\GtaRadio.Mod.dll"; DestDir: "{app}\scripts"; Components: mod; Flags: ignoreversion
Source: "payload\scripts\NAudio.Core.dll"; DestDir: "{app}\scripts"; Components: mod; Flags: ignoreversion
Source: "payload\scripts\NAudio.Wasapi.dll"; DestDir: "{app}\scripts"; Components: mod; Flags: ignoreversion
Source: "payload\scripts\GtaRadio.Mod.ini"; DestDir: "{app}\scripts"; Components: mod; Flags: onlyifdoesntexist uninsneveruninstall
Source: "payload\GtaRadio\*"; DestDir: "{app}\GtaRadio"; Components: mod; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "patch_station.ps1"; DestDir: "{app}\GtaRadio\tools"; Components: mod; Flags: ignoreversion
Source: "patch_logo.ps1"; DestDir: "{app}\GtaRadio\tools"; Components: mod; Flags: ignoreversion
Source: "logo_payload\*"; DestDir: "{app}\GtaRadio\logo"; Components: mod; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "art\aux_status.png"; DestDir: "{app}\GtaRadio"; DestName: "logo.png"; Components: mod; Flags: ignoreversion
; Загрузчики: только если их нет — у игрока может быть своя (в т.ч. более новая) версия.
Source: "payload\shv\ScriptHookV.dll"; DestDir: "{app}"; Components: shv; Flags: onlyifdoesntexist uninsneveruninstall
Source: "payload\shv\dinput8.dll"; DestDir: "{app}"; Components: shv; Flags: onlyifdoesntexist uninsneveruninstall; Check: NeedFile('dinput8.dll')
Source: "payload\shv\ScriptHookVDotNet.asi"; DestDir: "{app}"; Components: shv; Flags: onlyifdoesntexist uninsneveruninstall
Source: "payload\shv\ScriptHookVDotNet3.dll"; DestDir: "{app}"; Components: shv; Flags: onlyifdoesntexist uninsneveruninstall
Source: "payload\shv\ScriptHookVDotNet.ini"; DestDir: "{app}"; Components: shv; Flags: onlyifdoesntexist uninsneveruninstall
; SHVDN Enhanced без MinHook не стартует (DEEPSEEK-RESULT-2/3). ScriptHookVDotNet2.dll не ставим:
; с ним SHVDN не может писать лог (DEEPSEEK-RESULT-3), моду нужен только API v3.
Source: "payload\shv\MinHook.x64.dll"; DestDir: "{app}"; Components: shv; Flags: onlyifdoesntexist uninsneveruninstall

[UninstallDelete]
Type: filesandordirs; Name: "{app}\GtaRadio\tools"
Type: files; Name: "{app}\GtaRadio\GtaRadio.Agent.exe"

[UninstallRun]
; Агент без игры уже вернул громкость плеера; останавливаем, чтобы освободить exe.
Filename: "taskkill.exe"; Parameters: "/F /IM GtaRadio.Agent.exe"; Flags: runhidden waituntilterminated; RunOnceId: "StopAgent"
; Вернуть исходное название станции (только эту строку) до удаления файлов.
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\GtaRadio\tools\patch_station.ps1"" -GameDir ""{app}"" -Uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreStationName"
; Вернуть исходный логотип Pop в hud.ytd до удаления файлов.
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\GtaRadio\tools\patch_logo.ps1"" -GameDir ""{app}"" -Uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreLogo"

[Code]
function DefaultGameDir(Param: String): String;
var
  P: String;
begin
  Result := ExpandConstant('{autopf}\Rockstar Games\Grand Theft Auto V Enhanced');
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V Enhanced', 'InstallFolder', P) and FileExists(AddBackslash(P) + 'GTA5_Enhanced.exe') then
    Result := P
  else if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3240220', 'InstallLocation', P) and FileExists(AddBackslash(P) + 'GTA5_Enhanced.exe') then
    Result := P;
end;

function NeedFile(const Name: String): Boolean;
begin
  Result := not FileExists(AddBackslash(WizardDirValue) + Name);
end;

function IsProcessRunning(const Name: String): Boolean;
var
  Loc, Svc, Res: Variant;
begin
  Result := False;
  try
    Loc := CreateOleObject('WbemScripting.SWbemLocator');
    Svc := Loc.ConnectServer('.', 'root\CIMV2');
    Res := Svc.ExecQuery('SELECT Name FROM Win32_Process WHERE Name = "' + Name + '"');
    Result := Res.Count > 0;
  except
    Log('WMI: ' + GetExceptionMessage);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not FileExists(AddBackslash(WizardDirValue) + 'GTA5_Enhanced.exe') then
  begin
    SuppressibleMsgBox(ExpandConstant('{cm:NoGame}'), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if IsProcessRunning('GTA5_Enhanced.exe') or IsProcessRunning('PlayGTAV.exe') then
    Result := ExpandConstant('{cm:CloseGame}')
  else if IsProcessRunning('GtaRadio.Agent.exe') then
    Exec('taskkill.exe', '/F /IM GtaRadio.Agent.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  if IsProcessRunning('GTA5_Enhanced.exe') then
  begin
    SuppressibleMsgBox(ExpandConstant('{cm:GameRunningUnins}'), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

// Старый мод (GtaRadioMod.dll из версии 1.x) конфликтует с новым — убираем его с бэкапом.
procedure RemoveLegacy;
var
  Old, Bak: String;
begin
  Old := ExpandConstant('{app}\scripts\GtaRadioMod.dll');
  if FileExists(Old) then
  begin
    Bak := ExpandConstant('{app}\GtaRadio\backup\legacy');
    ForceDirectories(Bak);
    if RenameFile(Old, Bak + '\GtaRadioMod.dll') then Log('legacy mod moved to backup');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  if CurStep = ssInstall then RemoveLegacy;
  if (CurStep = ssPostInstall) and WizardIsComponentSelected('rename') then
  begin
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:StationStatus}');
    if not Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -File "' +
        ExpandConstant('{app}\GtaRadio\tools\patch_station.ps1') + '" -GameDir "' + ExpandConstant('{app}') + '"',
        '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      SuppressibleMsgBox(ExpandConstant('{cm:StationFail,' + IntToStr(Code) + '}'), mbError, MB_OK, IDOK);
  end;
  if (CurStep = ssPostInstall) and WizardIsComponentSelected('mod') then
  begin
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:LogoStatus}');
    if not Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -File "' +
        ExpandConstant('{app}\GtaRadio\tools\patch_logo.ps1') + '" -GameDir "' + ExpandConstant('{app}') + '"',
        '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      SuppressibleMsgBox(ExpandConstant('{cm:LogoFail,' + IntToStr(Code) + '}'), mbError, MB_OK, IDOK);
  end;
end;
