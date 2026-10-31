; ===========================================================================
;  Music Bar Win11 - установщик (Inno Setup 6)
;
;  Сборка:  iscc installer.iss
;  Результат: dist\MusicBarWin11-Setup-1.1.0.exe
;
;  Устанавливается БЕЗ прав администратора в %LOCALAPPDATA%\Programs,
;  конфиг хранится в %APPDATA%\MusicBarWin11. Автозапуск - в HKCU.
; ===========================================================================

#define AppName        "Music Bar Win11"
#define AppShortName   "MusicBarWin11"
#define AppVersion     "1.4.2"
#define AppPublisher   "Music Bar Win11"
#define AppExe         "MusicBarWin11.exe"
#define AppId          "{{8F3C1A52-7D64-4E19-9B2A-5C6E7A4D1F03}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}

; установка в пользовательский профиль: не нужен UAC и права администратора
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; платформа: Windows 10/11 x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

OutputDir=dist
OutputBaseFilename={#AppShortName}-Setup-{#AppVersion}
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=auto

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.AutostartTask=Запускать Music Bar Win11 при входе в Windows
russian.ExtraGroup=Дополнительно
russian.SettingsLink=Настройки виджета
russian.UninstallLink=Удалить Music Bar Win11
russian.AutostartPageCaption=Автозапуск
russian.AutostartPageDescription=Запускать виджет автоматически при входе в Windows?
russian.AutostartPageSubCaption=Виджет живёт на панели задач и запускается в трее. Автозапуск можно включить или выключить позже в настройках.
russian.AutostartYes=Да, запускать при входе в Windows (рекомендуется)
russian.AutostartNo=Нет, я запущу вручную

english.AutostartTask=Start Music Bar Win11 when Windows starts
english.ExtraGroup=Additional
english.SettingsLink=Widget settings
english.UninstallLink=Uninstall Music Bar Win11
english.AutostartPageCaption=Autostart
english.AutostartPageDescription=Start the widget automatically when Windows starts?
english.AutostartPageSubCaption=The widget lives on the taskbar. You can change this later in settings.
english.AutostartYes=Yes, start with Windows (recommended)
english.AutostartNo=No, I will start it manually

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:ExtraGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:SettingsLink}"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{group}\{cm:UninstallLink}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; первый запуск после установки
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; закрыть виджет перед удалением файлов
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "KillWidget"

[UninstallDelete]
; настройки пользователя НЕ удаляем: переживают переустановку/обновление
Type: filesandordirs; Name: "{app}\dist"

[Registry]
; автозапуск при входе в систему (HKCU, без прав администратора)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "{#AppShortName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Code]
var
  AutostartPage: TInputOptionWizardPage;

procedure InitializeWizard;
begin
  { страница «запускать при входе в Windows» }
  AutostartPage := CreateInputOptionPage(wpSelectTasks,
    ExpandConstant('{cm:AutostartPageCaption}'),
    ExpandConstant('{cm:AutostartPageDescription}'),
    ExpandConstant('{cm:AutostartPageSubCaption}'), True, False);
  AutostartPage.Add(ExpandConstant('{cm:AutostartYes}'));
  AutostartPage.Add(ExpandConstant('{cm:AutostartNo}'));
  AutostartPage.Values[0] := True;
  AutostartPage.Values[1] := False;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = AutostartPage.ID then
  begin
    if AutostartPage.Values[0] then
      WizardSelectTasks('autostart')
    else
      WizardSelectTasks('');
  end;
end;

{ при удалении: если виджет ещё прописан в HKCU\...\Run кем-то другим - снимаем }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RegKey: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegKey := 'Software\Microsoft\Windows\CurrentVersion\Run';
    RegDeleteValue(HKCU, RegKey, '{#AppShortName}');
  end;
end;
