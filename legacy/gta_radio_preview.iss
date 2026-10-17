; Установщик GTA Radio для GTA V Enhanced (Inno Setup 7).
; Шаблон — установщик нейроозвучки (music-studios/game-dub/games/gta5/installer/neurodub.iss):
; тёмный мастер, back/wizard/small картинки, компоненты, проверка папки игры, запуск после установки.
; Сборка: "F:\AI\InnoSetup\ISCC.exe" gta_radio.iss

#define AppVer "1.0"
#define AppGuid "{A7F3D2C1-6B84-4E21-9C55-2F8D1A7B3C90}"
#define OutBase "GtaRadio_Setup_" + AppVer

[Setup]
AppId={{#AppGuid}
AppName=GTA Radio
AppVersion={#AppVer}
AppVerName=GTA Radio {#AppVer} — Spotify и Яндекс Музыка в GTA V
AppPublisher=GTA Radio
DefaultDirName={code:DefaultGameDir}
DirExistsWarning=no
DisableWelcomePage=no
AppendDefaultDirName=no
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=8
OutputDir=output
OutputBaseFilename=GtaRadio_Preview
WizardStyle=modern dark includetitlebar hidebevels
WizardBackImageFile=art\back_radio.png
WizardImageFile=art\wizard_radio.png
WizardSmallImageFile=art\small_radio.png
UninstallFilesDir={app}\GtaRadio\uninstall
UninstallDisplayName=GTA Radio
UninstallDisplayIcon={app}\GtaRadio\GtaRadio.exe
SetupIconFile=..\app\GtaRadio\app.ico
SetupLogging=yes

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[Messages]
ru.SetupWindowTitle=Установка — GTA Radio
ru.WelcomeLabel1=GTA Radio
ru.WelcomeLabel2=Музыка из Spotify и Яндекс Музыки — как радиостанция в GTA V Enhanced.%n%nМод сам находит играющий плеер на компьютере (Spotify, Яндекс Музыка, AIMP, VLC, foobar2000 и другие), забирает звук, глушит оригинал и играет его в игре. В HUD видно источник, трек и исполнителя.%n%nФайлы игры и твоя озвучка не изменяются: мод ставится рядом и удаляется одной кнопкой. Перед заменой существующих файлов создаётся резервная копия.
ru.WizardSelectDir=Папка игры
ru.SelectDirDesc=Где установлена GTA V Enhanced?
ru.SelectDirLabel3=Укажите папку, где установлена GTA V Enhanced (в ней лежит GTA5_Enhanced.exe).
ru.SelectDirBrowseLabel=Нажмите «Далее», если папка верная, или «Обзор», чтобы выбрать другую.
ru.SelectComponentsDesc=Компоненты установки
ru.ReadyLabel1=Всё готово: мод, загрузчики и приложение будут установлены в папку игры.
ru.FinishedHeadingLabel=Готово!
ru.FinishedLabel=GTA Radio установлен.%n%nЗапусти игру, включи музыку в Spotify или Яндекс Музыке — агент сам подхватит её. Хоткеи в игре: F7 — вкл/выкл, F8 — предыдущий трек, F9 — пауза, F10 — следующий.%n%nЗначок GTA Radio живёт в трее и закрывается вместе с игрой.

[Types]
Name: "full"; Description: "Полная установка"
Name: "custom"; Description: "Выборочная установка"; Flags: iscustom

[Components]
Name: "mod"; Description: "Игровой мод GTA Radio (обязательно)"; Types: full custom; Flags: fixed
Name: "shv"; Description: "Script Hook V + ScriptHookVDotNet — нужны моду для работы в игре"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "Ярлык «GTA Radio» на рабочем столе"; GroupDescription: "Дополнительно:"

[Files]
; Приложение-агент (трей, автопоиск плеера, захват звука)
Source: "payload\GtaRadio\*"; DestDir: "{app}\GtaRadio"; Flags: ignoreversion recursesubdirs createallsubdirs
; Игровой мод
Source: "payload\GtaRadioMod.dll"; DestDir: "{app}\scripts"; Components: mod; Flags: ignoreversion
; Script Hook V + SHVDN (ставим, если ещё нет или если выбрано обновление)
Source: "payload\ScriptHookV.dll"; DestDir: "{app}"; Components: shv; Flags: ignoreversion
Source: "payload\ScriptHookVDotNet.asi"; DestDir: "{app}"; Components: shv; Flags: ignoreversion
Source: "payload\ScriptHookVDotNet3.dll"; DestDir: "{app}"; Components: shv; Flags: ignoreversion
Source: "payload\ScriptHookVDotNet.ini"; DestDir: "{app}"; Components: shv; Flags: ignoreversion
Source: "payload\MinHook.x64.dll"; DestDir: "{app}"; Components: shv; Flags: ignoreversion
; Загрузчик ASI — только если его ещё нет (у игрока может быть свой, более новый)
Source: "payload\xinput1_4.dll"; DestDir: "{app}"; Components: shv; Flags: ignoreversion; Check: NeedFile('xinput1_4.dll')
Source: "payload\dinput8.dll"; DestDir: "{app}"; Components: shv; Flags: ignoreversion; Check: NeedFile('dinput8.dll')
Source: "payload\args.txt"; DestDir: "{app}"; Components: shv; Flags: ignoreversion; Check: NeedFile('args.txt')

[Icons]
Name: "{commondesktop}\GTA Radio"; Filename: "{app}\GtaRadio\GtaRadio.exe"; Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{app}\GtaRadio"

[Run]
Filename: "{app}\GtaRadio\GtaRadio.exe"; Description: "Запустить GTA Radio"; Flags: postinstall nowait skipifsilent runasoriginaluser
Filename: "{app}\PlayGTAV.exe"; WorkingDir: "{app}"; Description: "Запустить GTA V"; Flags: postinstall nowait skipifsilent runasoriginaluser; Check: HasLauncher

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

function HasLauncher: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\PlayGTAV.exe'));
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
    SuppressibleMsgBox('В этой папке нет GTA5_Enhanced.exe. Укажите папку, где установлена GTA V Enhanced.', mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if IsProcessRunning('GTA5_Enhanced.exe') or IsProcessRunning('PlayGTAV.exe') then
    Result := 'Игра запущена. Закройте GTA V, затем нажмите «Назад» и снова «Установить».';
end;
