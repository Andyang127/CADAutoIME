#define MyAppName "CAD Auto IME"
#define MyAppVersion "0.3 （阶段性 - 终版）"

[Setup]
VersionInfoVersion=0.3.0.0
AppId={{9F8E2A1B-4C5D-6E7F-8A9B-0C1D2E3F4A5B}
PrivilegesRequired=lowest
AppName={#MyAppName}
AppVersion={#MyAppVersion}
OutputBaseFilename={#MyAppName} v{#MyAppVersion}
OutputDir=..\build\Release
DefaultDirName={userappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DirExistsWarning=no
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=Logo.ico
UninstallDisplayIcon={app}\logo.ico
Compression=lzma2
SolidCompression=yes

[Files]
; Sys17 - Sys27 各版本 DLL（逐一明确指定）
Source: "..\build\Sys17\OpenCadIme_Sys17.dll"; DestDir: "{app}\Sys17"; Flags: ignoreversion
Source: "..\build\Sys18\OpenCadIme_Sys18.dll"; DestDir: "{app}\Sys18"; Flags: ignoreversion
Source: "..\build\Sys19\OpenCadIme_Sys19.dll"; DestDir: "{app}\Sys19"; Flags: ignoreversion
Source: "..\build\Sys20\OpenCadIme_Sys20.dll"; DestDir: "{app}\Sys20"; Flags: ignoreversion
Source: "..\build\Sys21\OpenCadIme_Sys21.dll"; DestDir: "{app}\Sys21"; Flags: ignoreversion
Source: "..\build\Sys22\OpenCadIme_Sys22.dll"; DestDir: "{app}\Sys22"; Flags: ignoreversion
Source: "..\build\Sys23\OpenCadIme_Sys23.dll"; DestDir: "{app}\Sys23"; Flags: ignoreversion
Source: "..\build\Sys24\OpenCadIme_Sys24.dll"; DestDir: "{app}\Sys24"; Flags: ignoreversion
Source: "..\build\Sys25\OpenCadIme_Sys25.dll"; DestDir: "{app}\Sys25"; Flags: ignoreversion
Source: "..\build\Sys26\OpenCadIme_Sys26.dll"; DestDir: "{app}\Sys26"; Flags: ignoreversion
Source: "..\build\Sys27\OpenCadIme_Sys27.dll"; DestDir: "{app}\Sys27"; Flags: ignoreversion

; 说明文档和图标
Source: "Readme.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "Logo.ico"; DestDir: "{app}"; DestName: "logo.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\📖 CAD输入法助手-使用说明"; Filename: "{app}\Readme.html"; IconFilename: "{app}\logo.ico"
Name: "{group}\🗑️ 卸载 CAD输入法助手"; Filename: "{uninstallexe}"
Name: "{userdesktop}\[浅醉·墨语] CAD输入法助手"; Filename: "{app}\Readme.html"; IconFilename: "{app}\logo.ico"; Comment: "版本：v {#MyAppVersion}";

[Run]
Filename: "{app}\Readme.html"; Flags: postinstall shellexec skipifsilent

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[UninstallDelete]
; 卸载时删除各个版本文件夹
Type: filesandordirs; Name: "{app}\Sys17"
Type: filesandordirs; Name: "{app}\Sys18"
Type: filesandordirs; Name: "{app}\Sys19"
Type: filesandordirs; Name: "{app}\Sys20"
Type: filesandordirs; Name: "{app}\Sys21"
Type: filesandordirs; Name: "{app}\Sys22"
Type: filesandordirs; Name: "{app}\Sys23"
Type: filesandordirs; Name: "{app}\Sys24"
Type: filesandordirs; Name: "{app}\Sys25"
Type: filesandordirs; Name: "{app}\Sys26"
Type: filesandordirs; Name: "{app}\Sys27"
Type: files; Name: "{app}\AutoImeCommands.txt"
Type: files; Name: "{app}\Readme.html"
Type: files; Name: "{app}\logo.ico"

[Code]
// 全局变量
var
  G_DeleteUserData: Boolean;
  G_DeleteOldConfig: Boolean;

// 检测程序是否运行
function IsAppRunning(const FileName: string): Boolean;
var
  WbemLocator, WbemService, WbemObjectSet: Variant;
begin
  Result := False;
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemService := WbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    WbemObjectSet := WbemService.ExecQuery('SELECT * FROM Win32_Process WHERE Name="' + FileName + '"');
    Result := not VarIsNull(WbemObjectSet) and (WbemObjectSet.Count > 0);
  except
  end;
end;

// 强制关闭 CAD
procedure ForceKillCAD();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM acad.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM accoreconsole.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
end;

// 从路径中移除字符串
function RemoveStringFromPath(const FullPath, TargetStr: string): string;
var
  Res: string;
begin
  Res := FullPath;
  StringChangeEx(Res, ';' + TargetStr, '', True);
  StringChangeEx(Res, TargetStr + ';', '', True); 
  StringChangeEx(Res, TargetStr, '', True);
  StringChangeEx(Res, ';;', ';', True);
  Result := Res;
end;

// 安装初始化
function InitializeSetup(): Boolean;
var
  OldVersion, UninstallString, NewVersion, OldInstallPath: string;
  ResultCode: Integer;
  UninstKey, AcadKey: string;
begin
  Result := True;
  G_DeleteOldConfig := False;

  // ==============================================
  // 防呆检测：检查是否安装了 AutoCAD
  // ==============================================
  AcadKey := 'Software\Autodesk\AutoCAD';
  if (not RegKeyExists(HKEY_CURRENT_USER, AcadKey)) and (not RegKeyExists(HKEY_LOCAL_MACHINE, AcadKey)) then
  begin
    MsgBox('⛔ 程序安装拦截！' + #13#10 + #13#10 +
           '系统中未检测到任何 AutoCAD 环境。' + #13#10 + #13#10 +
           '本程序必须依赖 AutoCAD 运行，请【先安装 AutoCAD 并至少运行过一次】，然后再运行本安装程序！', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  NewVersion := '{#SetupSetting("AppVersion")}';
  UninstKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{9F8E2A1B-4C5D-6E7F-8A9B-0C1D2E3F4A5B}_is1';
  
  // ==============================================
  // 检测 CAD 是否正在运行
  // ==============================================
  if IsAppRunning('acad.exe') then
  begin
    if MsgBox('安装程序检测到 [ AutoCAD ] 正在运行中。' + #13#10 + #13#10 +
              '为保证插件底层核心能够顺利写入并生效，必须先关闭 AutoCAD。' + #13#10 +#13#10 +
              '▶ 请先保存好你的图纸！' + #13#10 + #13#10 +
              '你是否已保存完毕，并允许安装程序立即关闭 AutoCAD？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ForceKillCAD();
    end else begin
      Result := False;
      Exit;
    end;
  end;

  // ==============================================
  // 覆盖安装处理
  // ==============================================
  if RegQueryStringValue(HKEY_CURRENT_USER, UninstKey, 'DisplayVersion', OldVersion) then
  begin
    case MsgBox('检测到系统已安装[CAD Auto IME] v' + OldVersion + '。' + #13#10 + #13#10 +
              '请选择覆盖安装方式：' + #13#10 + #13#10 +
              '✅ 是(Y)：保留我的自定义白名单配置（推荐）' + #13#10 + #13#10 +
              '❌ 否(N)：彻底删除旧配置，全新安装' + #13#10 + #13#10 +
              '取消：退出安装', mbConfirmation, MB_YESNOCANCEL) of
      IDYES:
        begin
          G_DeleteOldConfig := False;
          if RegQueryStringValue(HKEY_CURRENT_USER, UninstKey, 'UninstallString', UninstallString) then
          begin
            UninstallString := RemoveQuotes(UninstallString);
            Exec(UninstallString, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
            Sleep(1000); 
          end;
        end;
      IDNO:
        begin
          G_DeleteOldConfig := True;
          if RegQueryStringValue(HKEY_CURRENT_USER, UninstKey, 'InstallLocation', OldInstallPath) then
          begin
            OldInstallPath := RemoveQuotes(OldInstallPath);
            if FileExists(OldInstallPath + '\AutoImeCommands.txt') then
              DeleteFile(OldInstallPath + '\AutoImeCommands.txt');
            RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\QianZuiMoYu\CADAutoIme');
          end;
          if RegQueryStringValue(HKEY_CURRENT_USER, UninstKey, 'UninstallString', UninstallString) then
          begin
            UninstallString := RemoveQuotes(UninstallString);
            Exec(UninstallString, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
            Sleep(1000); 
          end;
        end;
      IDCANCEL:
        begin
          Result := False;
          Exit;
        end;
    end;
  end;
end;

// 卸载初始化
function InitializeUninstall(): Boolean;
begin
  Result := True;
  G_DeleteUserData := False;

  if UninstallSilent then Exit;

  if IsAppRunning('acad.exe') then
  begin
    if MsgBox('卸载程序检测到 [ AutoCAD ] 正在运行中。' + #13#10 +#13#10 + #13#10 +
              '为确保文件彻底清除，必须先关闭 AutoCAD。是否允许立即关闭？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ForceKillCAD();
    end else begin
      Result := False;
      Exit;
    end;
  end;

  if MsgBox('即将卸载 CAD Auto IME。' + #13#10 + #13#10 + 
            '是否要彻底清除所有数据？' + #13#10 + #13#10 +
            '(选择"是"将连同你自定义的快捷键白名单一并删除)', mbConfirmation, MB_YESNO) = IDYES then
  begin
    G_DeleteUserData := True;
  end;
end;

// 安装后写入注册表（自动加载插件）
procedure CurStepChanged(CurStep: TSetupStep);
var
  AcadKey, RKey, AcadInstKey, VariablesKey, AppKey: string;
  RNames, InstNames, ProfileNames: TArrayOfString;
  i, j, k: Integer;
  CurrentPath, NewPath, InstallPath, TrustedPathFormat, SysVer, DllPath: string;
begin
  if CurStep = ssPostInstall then
  begin
    InstallPath := ExpandConstant('{app}');
    TrustedPathFormat := InstallPath + '\...';
    AcadKey := 'Software\Autodesk\AutoCAD';

    if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadKey, RNames) then
    begin
      for i := 0 to GetArrayLength(RNames)-1 do
      begin
        SysVer := '';
        if Pos('R17', RNames[i]) > 0 then SysVer := '17'
        else if Pos('R18', RNames[i]) > 0 then SysVer := '18'
        else if Pos('R19', RNames[i]) > 0 then SysVer := '19'
        else if Pos('R20', RNames[i]) > 0 then SysVer := '20'
        else if Pos('R21', RNames[i]) > 0 then SysVer := '21'
        else if Pos('R22', RNames[i]) > 0 then SysVer := '22'
        else if Pos('R23', RNames[i]) > 0 then SysVer := '23'
        else if Pos('R24', RNames[i]) > 0 then SysVer := '24'
        else if Pos('R25', RNames[i]) > 0 then SysVer := '25' 
        else if Pos('R26', RNames[i]) > 0 then SysVer := '26' 
        else if Pos('R27', RNames[i]) > 0 then SysVer := '27';

        if SysVer <> '' then
        begin
          RKey := AcadKey + '\' + RNames[i];
          if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
          begin
            for j := 0 to GetArrayLength(InstNames)-1 do
            begin
              AppKey := RKey + '\' + InstNames[j] + '\Applications\CADAutoIme';
              DllPath := InstallPath + '\Sys' + SysVer + '\OpenCadIme_Sys' + SysVer + '.dll';

              if FileExists(DllPath) then
              begin
                RegWriteDWordValue(HKEY_CURRENT_USER, AppKey, 'LOADCTRLS', 2);
                RegWriteDWordValue(HKEY_CURRENT_USER, AppKey, 'MANAGED', 1);
                RegWriteStringValue(HKEY_CURRENT_USER, AppKey, 'LOADER', DllPath);
                RegWriteStringValue(HKEY_CURRENT_USER, AppKey, 'DESCRIPTION', 'CAD Auto Input Method Engine');

                AcadInstKey := RKey + '\' + InstNames[j] + '\Profiles';
                if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadInstKey, ProfileNames) then
                begin
                  for k := 0 to GetArrayLength(ProfileNames)-1 do
                  begin
                    VariablesKey := AcadInstKey + '\' + ProfileNames[k] + '\Variables';

                    if RegQueryStringValue(HKEY_CURRENT_USER, VariablesKey, 'ACAD', CurrentPath) then
                    begin
                      if Pos(Lowercase(InstallPath), Lowercase(CurrentPath)) = 0 then
                      begin
                        NewPath := CurrentPath + ';' + InstallPath;
                        RegWriteStringValue(HKEY_CURRENT_USER, VariablesKey, 'ACAD', NewPath);
                      end;
                    end;

                    if RegQueryStringValue(HKEY_CURRENT_USER, VariablesKey, 'TRUSTEDPATHS', CurrentPath) then
                    begin
                      if Pos(Lowercase(InstallPath), Lowercase(CurrentPath)) = 0 then
                      begin
                        NewPath := CurrentPath + ';' + TrustedPathFormat;
                        RegWriteStringValue(HKEY_CURRENT_USER, VariablesKey, 'TRUSTEDPATHS', NewPath);
                      end;
                    end else begin
                      RegWriteStringValue(HKEY_CURRENT_USER, VariablesKey, 'TRUSTEDPATHS', TrustedPathFormat);
                    end;
                  end;
                end;
              end;
            end;
          end;
        end;
      end;
    end;
  end;
end;

// 卸载时清理注册表
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AcadKey, RKey, AcadInstKey, VariablesKey, AppKey: string;
  RNames, InstNames, ProfileNames: TArrayOfString;
  i, j, k: Integer;
  CurrentPath, NewPath, InstallPath, TrustedPathFormat: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\QianZuiMoYu\CADAutoIme');

    InstallPath := ExpandConstant('{app}');
    TrustedPathFormat := InstallPath + '\...'; 
    AcadKey := 'Software\Autodesk\AutoCAD';

    if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadKey, RNames) then
    begin
      for i := 0 to GetArrayLength(RNames)-1 do
      begin
        if (Pos('R17', RNames[i]) > 0) or (Pos('R18', RNames[i]) > 0) or 
           (Pos('R19', RNames[i]) > 0) or (Pos('R20', RNames[i]) > 0) or 
           (Pos('R21', RNames[i]) > 0) or (Pos('R22', RNames[i]) > 0) or 
           (Pos('R23', RNames[i]) > 0) or (Pos('R24', RNames[i]) > 0) or 
           (Pos('R25', RNames[i]) > 0) or (Pos('R26', RNames[i]) > 0) or 
           (Pos('R27', RNames[i]) > 0) then
        begin
          RKey := AcadKey + '\' + RNames[i];
          if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
          begin
            for j := 0 to GetArrayLength(InstNames)-1 do
            begin
              AppKey := RKey + '\' + InstNames[j] + '\Applications\CADAutoIme';
              RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, AppKey);

              AcadInstKey := RKey + '\' + InstNames[j] + '\Profiles';
              if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadInstKey, ProfileNames) then
              begin
                for k := 0 to GetArrayLength(ProfileNames)-1 do
                begin
                  VariablesKey := AcadInstKey + '\' + ProfileNames[k] + '\Variables';

                  if RegQueryStringValue(HKEY_CURRENT_USER, VariablesKey, 'ACAD', CurrentPath) then
                  begin
                    NewPath := RemoveStringFromPath(CurrentPath, InstallPath);
                    if NewPath <> CurrentPath then
                      RegWriteStringValue(HKEY_CURRENT_USER, VariablesKey, 'ACAD', NewPath);
                  end;

                  if RegQueryStringValue(HKEY_CURRENT_USER, VariablesKey, 'TRUSTEDPATHS', CurrentPath) then
                  begin
                    NewPath := RemoveStringFromPath(CurrentPath, TrustedPathFormat);
                    if NewPath <> CurrentPath then
                      RegWriteStringValue(HKEY_CURRENT_USER, VariablesKey, 'TRUSTEDPATHS', NewPath);
                  end;
                end;
              end;
            end;
          end;
        end;
      end;
    end;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    if G_DeleteUserData then
    begin
      DelTree(ExpandConstant('{app}'), True, True, True);
    end 
    else 
    begin
      RemoveDir(ExpandConstant('{app}'));
    end;
  end;
end;
