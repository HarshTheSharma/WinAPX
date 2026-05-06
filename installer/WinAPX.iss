[Setup]
AppId={{B14C0B8E-9F3E-4D33-9E6E-WINAPX0001}}
AppName=WinAPX
AppVersion=1.0.0
AppPublisher=Harsh
DefaultDirName={autopf}\WinAPX
DefaultGroupName=WinAPX
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible or arm64
ArchitecturesInstallIn64BitMode=x64compatible or arm64
ChangesEnvironment=yes
OutputDir=output
OutputBaseFilename=WinAPX-Setup
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
SetupIconFile=WinAPX.ico
UninstallDisplayIcon={app}\GUI\WinAPX.Gui.exe

[Types]
Name: "full";   Description: "Full installation (GUI + CLI)"
Name: "gui";    Description: "GUI only"
Name: "cli";    Description: "CLI only"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "gui"; Description: "WinAPX GUI application";   Types: full gui custom
Name: "cli"; Description: "WinAPX command-line tool"; Types: full cli custom

[Tasks]
Name: "desktopicon";   Description: "Create a desktop shortcut";    GroupDescription: "Additional shortcuts:"; Components: gui
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "Additional shortcuts:"; Components: gui; Flags: checkedonce

[Files]
Source: "..\publish\gui\x64\*";          DestDir: "{app}\GUI"; Flags: recursesubdirs ignoreversion; Components: gui; Check: not IsArm64
Source: "..\publish\cli\x64\winapx.exe"; DestDir: "{app}";    Flags: ignoreversion;                 Components: cli; Check: not IsArm64
Source: "..\publish\gui\arm64\*";          DestDir: "{app}\GUI"; Flags: recursesubdirs ignoreversion; Components: gui; Check: IsArm64
Source: "..\publish\cli\arm64\winapx.exe"; DestDir: "{app}";    Flags: ignoreversion;                 Components: cli; Check: IsArm64

[Icons]
Name: "{group}\WinAPX";           Filename: "{app}\GUI\WinAPX.Gui.exe"; Components: gui; Tasks: startmenuicon
Name: "{group}\Uninstall WinAPX"; Filename: "{uninstallexe}"
Name: "{commondesktop}\WinAPX";   Filename: "{app}\GUI\WinAPX.Gui.exe"; Components: gui; Tasks: desktopicon

[Run]
Filename: "{cmd}"; Parameters: "/c copy /Y ""{app}\winapx.exe"" ""{app}\apx.exe"""; \
    Components: cli; Flags: runhidden
Filename: "{cmd}"; Parameters: "/c echo %LOCALAPPDATA% > ""{app}\.userdata-path"""; \
    Flags: runhidden runasoriginaluser

[UninstallRun]
Filename: "wsl.exe"; Parameters: "--unregister WinAPX-Template-Ubuntu"; Flags: runhidden; RunOnceId: "RemoveUbuntuTemplate"
Filename: "wsl.exe"; Parameters: "--unregister WinAPX-Template-Arch";   Flags: runhidden; RunOnceId: "RemoveArchTemplate"

[UninstallDelete]
Type: files;          Name: "{app}\.userdata-path"
Type: files;          Name: "{app}\apx.exe"
Type: filesandordirs; Name: "{commonappdata}\apx-wsl\templates"

[Code]
const
  EnvKey = 'System\CurrentControlSet\Control\Session Manager\Environment';

function RunWsl(const Args: string; out ExitCode: Integer): Boolean;
begin
  Result := Exec('wsl.exe', Args, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
end;

function RunWslVisible(const Args: string; out ExitCode: Integer): Boolean;
begin
  Result := Exec('wsl.exe', Args, '', SW_SHOW, ewWaitUntilTerminated, ExitCode);
end;

function IsWslReady(): Boolean;
var
  Code: Integer;
begin
  Result := RunWsl('--status', Code) and (Code = 0);
end;

function DistroExists(const Name: string): Boolean;
var
  Code: Integer;
  TempFile: string;
  Lines: TArrayOfString;
  i: Integer;
  CleanedLine: string;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\wsl-list.txt');
  Exec(ExpandConstant('{cmd}'),
       '/c wsl.exe --list --quiet > "' + TempFile + '"',
       '', SW_HIDE, ewWaitUntilTerminated, Code);
  if not LoadStringsFromFile(TempFile, Lines) then Exit;
  for i := 0 to GetArrayLength(Lines) - 1 do
  begin
    CleanedLine := Lines[i];
    StringChangeEx(CleanedLine, #0, '', True);
    CleanedLine := Trim(CleanedLine);
    if SameText(CleanedLine, Name) then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure SetPreparingStatus(const Msg: string);
begin
  if WizardForm <> nil then
  begin
    WizardForm.PreparingLabel.Caption := Msg;
    WizardForm.Refresh;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if not IsWslReady() then
  begin
    MsgBox(
      'WinAPX requires the Windows Subsystem for Linux (WSL) to be set up before installation.' + #13#10 + #13#10 +
      'Please:' + #13#10 +
      '  1. Open an administrator PowerShell or Command Prompt' + #13#10 +
      '  2. Run:  wsl --install --no-distribution' + #13#10 +
      '  3. Restart your computer' + #13#10 +
      '  4. Run this installer again' + #13#10 + #13#10 +
      'Setup will now exit.',
      mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;

  if DistroExists('Ubuntu') or DistroExists('archlinux') then
  begin
    if MsgBox(
      'You already have an Ubuntu or Arch Linux WSL distribution installed.' + #13#10 + #13#10 +
      'WinAPX needs to register its own template versions of these distros, ' +
      'which requires unregistering the existing ones. ANY DATA IN YOUR EXISTING ' +
      'Ubuntu OR archlinux WSL DISTRIBUTIONS WILL BE LOST.' + #13#10 + #13#10 +
      'If you have important data in these distros, cancel now and back it up with:' + #13#10 +
      '  wsl --export <name> <out.tar>' + #13#10 + #13#10 +
      'Continue with installation?',
      mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function SetupTemplate(const SourceName, TemplateName, ImportSubdir, DisplayName: string): string;
var
  Code, RecoveryCode: Integer;
  TarPath, ImportPath, RecoverPath: string;
  RecoveryOk: Boolean;
begin
  Result := '';

  TarPath := ExpandConstant('{tmp}\') + ImportSubdir + '.tar';
  ImportPath := ExpandConstant('{commonappdata}\apx-wsl\templates\') + ImportSubdir;
  RecoverPath := ExpandConstant('{commonappdata}\apx-wsl\templates\') + ImportSubdir + '_recover';

  if DistroExists(TemplateName) then
  begin
    Result := '';
    Exit;
  end;

  if DistroExists(SourceName) then
  begin
    SetPreparingStatus('Removing existing ' + SourceName + ' distribution...');
    RunWsl('--unregister ' + SourceName, Code);
    if DistroExists(SourceName) then
    begin
      Result := 'Could not unregister your existing ' + SourceName + ' distribution.';
      Exit;
    end;
  end;

  SetPreparingStatus('Downloading ' + DisplayName + ' template (this may take several minutes)...');
  RunWslVisible('--install -d ' + SourceName + ' --no-launch', Code);
  if not DistroExists(SourceName) then
  begin
    Result := 'Failed to download ' + DisplayName + ' (exit code ' + IntToStr(Code) + '). Check your internet connection and that WSL is fully set up, then try again.';
    Exit;
  end;

  SetPreparingStatus('Preparing ' + DisplayName + ' template...');
  RunWsl('--export ' + SourceName + ' "' + TarPath + '"', Code);
  if not FileExists(TarPath) then
  begin
    RunWsl('--unregister ' + SourceName, RecoveryCode);
    Result := 'Failed to export ' + DisplayName + ' template (exit code ' + IntToStr(Code) + ').';
    Exit;
  end;

  RunWsl('--unregister ' + SourceName, Code);
  if DistroExists(SourceName) then
  begin
    DeleteFile(TarPath);
    Result := 'Failed to clean up source ' + DisplayName + ' distribution.';
    Exit;
  end;

  SetPreparingStatus('Registering ' + DisplayName + ' template...');
  ForceDirectories(ImportPath);
  RunWsl('--import ' + TemplateName + ' "' + ImportPath + '" "' + TarPath + '"', Code);
  if not DistroExists(TemplateName) then
  begin
    ForceDirectories(RecoverPath);
    RunWsl('--import ' + SourceName + ' "' + RecoverPath + '" "' + TarPath + '"', RecoveryCode);
    RecoveryOk := DistroExists(SourceName);
    if RecoveryOk then
    begin
      DeleteFile(TarPath);
      Result := 'Failed to register ' + DisplayName + ' template (exit code ' + IntToStr(Code) + '). Your original ' + SourceName + ' distribution has been restored.';
    end
    else
    begin
      Result := 'Failed to register ' + DisplayName + ' template (exit code ' + IntToStr(Code) + '). Automatic recovery also failed. Your distribution backup is at: ' + TarPath + '. To restore manually: wsl --import ' + SourceName + ' <path-for-distro> "' + TarPath + '"';
    end;
    Exit;
  end;

  DeleteFile(TarPath);
end;

procedure CleanupLeftoverState();
var
  Code: Integer;
begin
  SetPreparingStatus('Cleaning up any leftover state from previous installs...');
  RunWsl('--unregister WinAPX-Template-Ubuntu', Code);
  RunWsl('--unregister WinAPX-Template-Arch', Code);
  DelTree(ExpandConstant('{commonappdata}\apx-wsl\templates'), True, True, True);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CleanupLeftoverState();

  Result := SetupTemplate('Ubuntu', 'WinAPX-Template-Ubuntu', 'ubuntu', 'Ubuntu');
  if Result <> '' then Exit;

  Result := SetupTemplate('archlinux', 'WinAPX-Template-Arch', 'arch', 'Arch Linux');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Path: string;
begin
  if CurStep = ssPostInstall then
  begin
    if IsComponentSelected('cli') then
    begin
      if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvKey, 'Path', Path) then
        Path := '';
      if Pos(';' + LowerCase(ExpandConstant('{app}')) + ';',
             ';' + LowerCase(Path) + ';') = 0 then
      begin
        if (Path <> '') and (Path[Length(Path)] <> ';') then
          Path := Path + ';';
        Path := Path + ExpandConstant('{app}');
        RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvKey, 'Path', Path);
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Path, AppDir, Needle: string;
  P: Integer;
  PathFile: string;
  PathLines: TArrayOfString;
  UserLocalAppData, UserApxWsl: string;
  Removed: Boolean;
begin
  Removed := False;

  if CurUninstallStep = usUninstall then
  begin
    PathFile := ExpandConstant('{app}\.userdata-path');
    if FileExists(PathFile) and LoadStringsFromFile(PathFile, PathLines) then
    begin
      if GetArrayLength(PathLines) > 0 then
      begin
        UserLocalAppData := Trim(PathLines[0]);
        if UserLocalAppData <> '' then
        begin
          UserApxWsl := AddBackslash(UserLocalAppData) + 'apx-wsl';
          if DirExists(UserApxWsl) then
          begin
            if MsgBox(
              'Also remove WinAPX user data?' + #13#10 + #13#10 +
              'This will delete:' + #13#10 +
              '  ' + UserApxWsl + #13#10 + #13#10 +
              'Including all environments, exported app metadata, and configuration. ' +
              'WSL distributions registered through WinAPX will become unusable; ' +
              'remove them manually with:' + #13#10 +
              '  wsl --unregister <name>',
              mbConfirmation, MB_YESNO) = IDYES then
            begin
              if DelTree(UserApxWsl, True, True, True) then
                Removed := True;
            end;
          end;
        end;
      end;
    end;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvKey, 'Path', Path) then
    begin
      AppDir := ExpandConstant('{app}');
      Needle := ';' + LowerCase(AppDir) + ';';
      P := Pos(Needle, ';' + LowerCase(Path) + ';');
      if P > 0 then
      begin
        Delete(Path, P, Length(AppDir) + 1);
        if (Path <> '') and (Path[1] = ';') then
          Delete(Path, 1, 1);
        RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, EnvKey, 'Path', Path);
      end;
    end;

    if Removed then
    begin
      MsgBox(
        'WinAPX has been uninstalled and your user data has been removed.' + #13#10 + #13#10 +
        'Note: WSL distributions you created through WinAPX are still registered. ' +
        'To remove them:' + #13#10 +
        '  1. Open PowerShell or Command Prompt' + #13#10 +
        '  2. Run:  wsl --list' + #13#10 +
        '  3. Run:  wsl --unregister <name>  for each one you want gone.',
        mbInformation, MB_OK);
    end
    else
    begin
      MsgBox(
        'WinAPX has been uninstalled. Your user data has been preserved.' + #13#10 + #13#10 +
        'If you want to remove it later:' + #13#10 +
        '  1. Delete:  %LOCALAPPDATA%\apx-wsl' + #13#10 +
        '  2. Run:  wsl --list  to see remaining distributions.' + #13#10 +
        '  3. Run:  wsl --unregister <name>  to remove WinAPX environments.' + #13#10 +
        '  4. Remove any leftover Desktop shortcuts.',
        mbInformation, MB_OK);
    end;
  end;
end;