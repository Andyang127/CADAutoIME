#define MyAppNameZh "智能中英输入法自动切换 (CAD Auto IME)"
#define MyAppNameEn "CadAutoIme"
#define MyAppVersion "0.4.3"
#define MyPublisher "Mr.yang"
#define DllPrefix "OpenCadIme"
#define MyAppDirName "OpenCadIme"

; 输出路径
#define OutputPath "..\build"

[Setup]
VersionInfoVersion={#MyAppVersion}
AppId={{9F8E2A1B-4C5D-6E7F-8A9B-0C1D2E3F4A5B}}
PrivilegesRequired=lowest

AppName={#MyAppNameZh}
AppVerName={#MyAppNameZh} v{#MyAppVersion}
AppPublisher={#MyPublisher}

OutputBaseFilename={#MyAppNameEn}_Setup_v{#MyAppVersion}
OutputDir=..\build\Installer

DefaultDirName={userappdata}\{#MyAppDirName}
DefaultGroupName={#MyAppNameZh}
DisableProgramGroupPage=no
AllowNoIcons=yes

DirExistsWarning=no
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=Icon.ico
UninstallDisplayIcon={app}\Icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMADictionarySize=1048576
RestartIfNeededByRun=no

[Files]
Source: "Icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "Readme.html"; DestDir: "{app}"; Flags: ignoreversion

; Sys17 ~ Sys27 核心引擎 DLL 部署
Source: "{#OutputPath}\Sys17\*"; DestDir: "{app}\Sys17"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys18\*"; DestDir: "{app}\Sys18"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys19\*"; DestDir: "{app}\Sys19"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys20\*"; DestDir: "{app}\Sys20"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys21\*"; DestDir: "{app}\Sys21"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys22\*"; DestDir: "{app}\Sys22"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys23\*"; DestDir: "{app}\Sys23"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys24\*"; DestDir: "{app}\Sys24"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys25\*"; DestDir: "{app}\Sys25"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys26\*"; DestDir: "{app}\Sys26"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\Sys27\*"; DestDir: "{app}\Sys27"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Tasks]
Name: "desktopicon"; Description: "创建使用说明桌面快捷方式"; GroupDescription: "附加选项:"; Flags: checkablealone

[Icons]
Name: "{group}\📖 {#MyAppNameZh} - 使用说明"; Filename: "{app}\Readme.html"; IconFilename: "{app}\Icon.ico"; Comment: "查看使用说明与命令列表"
Name: "{group}\📁 {#MyAppNameZh} - 打开安装目录"; Filename: "{app}"; Comment: "打开安装文件夹"
Name: "{group}\🗑️ 卸载 {#MyAppNameZh}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Icon.ico"; Comment: "卸载输入法切换插件"
Name: "{autodesktop}\{#MyAppNameZh}"; Filename: "{app}\Readme.html"; IconFilename: "{app}\Icon.ico"; Comment: "{#MyAppNameZh}"; Tasks: desktopicon

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Run]
Filename: "{app}\Readme.html"; Description: "立即查看使用说明 (图文手册)"; Flags: postinstall shellexec skipifsilent


[Code]
var
  AcadVersionPage: TInputOptionWizardPage;
  FoundRNames: TStringList;
  G_DeleteUserData: Boolean;

// ==========================================
// 🛡️ 进程防护模块 (防止 DLL 锁死)
// ==========================================
function IsAppRunning(const FileName: string): Boolean;
var
  WbemLocator, WbemService, WbemObjectSet: Variant;
begin
  Result := False;
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemService := WbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    WbemObjectSet := WbemService.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="' + FileName + '"');
    Result := not VarIsNull(WbemObjectSet) and (WbemObjectSet.Count > 0);
  except
  end;
end;

procedure ForceKillCAD();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM acad.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM accoreconsole.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1500);
end;

// ==========================================
// 🧹 路径列表安全工具 (处理分号与路径合并)
// ==========================================
function AddToPathList(const PathList, NewPath: string): string;
var
  SearchStr, Target: string;
begin
  SearchStr := ';' + Lowercase(Trim(PathList)) + ';';
  Target := ';' + Lowercase(Trim(NewPath)) + ';';
  if Pos(Target, SearchStr) = 0 then
  begin
    if Trim(PathList) = '' then Result := Trim(NewPath)
    else Result := Trim(PathList) + ';' + Trim(NewPath);
  end else Result := PathList;
end;

function RemoveFromPathList(const PathList, TargetPath: string): string;
var
  P: Integer;
  TempList, CurrentPath, Res, TargetL: string;
begin
  TempList := PathList + ';';
  Res := '';
  TargetL := Lowercase(Trim(TargetPath));
  P := Pos(';', TempList);
  while P > 0 do
  begin
    CurrentPath := Trim(Copy(TempList, 1, P - 1));
    if (CurrentPath <> '') and (Lowercase(CurrentPath) <> TargetL) then
    begin
      if Res = '' then Res := CurrentPath
      else Res := Res + ';' + CurrentPath;
    end;
    Delete(TempList, 1, P);
    P := Pos(';', TempList);
  end;
  Result := Res;
end;

// ==========================================
// 🔍 AutoCAD 版本世代与显示名归一化
// ==========================================
function GetBaseRFromRName(const RName: string; out DisplayName: string): string;
begin
  Result := '';
  DisplayName := '';
  if Pos('R17', RName) > 0 then begin DisplayName := 'AutoCAD 2007 - 2009'; Result := 'R17'; end
  else if Pos('R18', RName) > 0 then begin DisplayName := 'AutoCAD 2010 - 2012'; Result := 'R18'; end
  else if Pos('R19', RName) > 0 then begin DisplayName := 'AutoCAD 2013 - 2014'; Result := 'R19'; end
  else if Pos('R20', RName) > 0 then begin DisplayName := 'AutoCAD 2015 - 2016'; Result := 'R20'; end
  else if Pos('R21', RName) > 0 then begin DisplayName := 'AutoCAD 2017'; Result := 'R21'; end
  else if Pos('R22', RName) > 0 then begin DisplayName := 'AutoCAD 2018'; Result := 'R22'; end
  else if Pos('R23', RName) > 0 then begin DisplayName := 'AutoCAD 2019 - 2020'; Result := 'R23'; end
  else if Pos('R24', RName) > 0 then begin DisplayName := 'AutoCAD 2021 - 2024'; Result := 'R24'; end
  else if Pos('R25.0', RName) > 0 then begin DisplayName := 'AutoCAD 2025'; Result := 'R25'; end
  else if Pos('R25.1', RName) > 0 then begin DisplayName := 'AutoCAD 2026'; Result := 'R26'; end
  else if Pos('R26.0', RName) > 0 then begin DisplayName := 'AutoCAD 2027'; Result := 'R27'; end
  else if Pos('R25', RName) > 0 then begin DisplayName := 'AutoCAD 2025'; Result := 'R25'; end
  else if Pos('R26', RName) > 0 then begin DisplayName := 'AutoCAD 2026'; Result := 'R26'; end
  else if Pos('R27', RName) > 0 then begin DisplayName := 'AutoCAD 2027'; Result := 'R27'; end;
end;

// ==========================================
// 🎯 精准寻找对应世代的 DLL 物理路径
// ==========================================
function FindDllForBaseR(const InstallRoot, BaseR: string): string;
var
  SysR, SysNum, Candidate: string;
begin
  Result := '';
  SysNum := Copy(BaseR, 2, Length(BaseR) - 1);
  SysR := 'Sys' + SysNum;

  // 1. 优先匹配 SysXX 目录下的标准命名
  Candidate := InstallRoot + '\' + SysR + '\{#DllPrefix}_' + SysR + '.dll';
  if FileExists(Candidate) then begin Result := Candidate; Exit; end;

  // 2. 针对 2025+ 特殊命名匹配
  if BaseR = 'R25' then
  begin
    Candidate := InstallRoot + '\' + SysR + '\{#DllPrefix}_R25.0_CAD_2025.dll';
    if FileExists(Candidate) then begin Result := Candidate; Exit; end;
  end
  else if BaseR = 'R26' then
  begin
    Candidate := InstallRoot + '\' + SysR + '\{#DllPrefix}_R25.1_CAD_2026.dll';
    if FileExists(Candidate) then begin Result := Candidate; Exit; end;
  end
  else if BaseR = 'R27' then
  begin
    Candidate := InstallRoot + '\' + SysR + '\{#DllPrefix}_R26.0_CAD_2027.dll';
    if FileExists(Candidate) then begin Result := Candidate; Exit; end;
  end;

  // 3. 兼容 Rxx 目录命名回退
  Candidate := InstallRoot + '\' + BaseR + '\{#DllPrefix}_' + BaseR + '.dll';
  if FileExists(Candidate) then begin Result := Candidate; Exit; end;
  
  Candidate := InstallRoot + '\' + BaseR + '\{#DllPrefix}.dll';
  if FileExists(Candidate) then begin Result := Candidate; Exit; end;
end;

// ==========================================
// ⚙️ CAD 单实例注册/反注册公共过程
// ==========================================
procedure ConfigureAcadProfile(const ProfileKey, InstallRoot, SysDir, RootTrustedFormat, SysTrustedFormat: string; IsInstall: Boolean);
var
  CurrentPath, NewPath: string;
begin
  // 1. 注册支持搜索路径 (ACAD)
  if RegQueryStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'ACAD', CurrentPath) then
  begin
    if IsInstall then
    begin
      NewPath := AddToPathList(CurrentPath, InstallRoot);
      NewPath := AddToPathList(NewPath, SysDir);
    end else begin
      NewPath := RemoveFromPathList(CurrentPath, InstallRoot);
      NewPath := RemoveFromPathList(NewPath, SysDir);
    end;
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'ACAD', NewPath);
  end;

  // 2. 注册受信任路径 (TRUSTEDPATHS) - 同时注册 Variables 与 General 节点，杜绝安全拦截警告
  if RegQueryStringValue(HKEY_CURRENT_USER, ProfileKey + '\Variables', 'TRUSTEDPATHS', CurrentPath) then
  begin
    if IsInstall then
    begin
      NewPath := AddToPathList(CurrentPath, RootTrustedFormat);
      NewPath := AddToPathList(NewPath, SysTrustedFormat);
    end else begin
      NewPath := RemoveFromPathList(CurrentPath, RootTrustedFormat);
      NewPath := RemoveFromPathList(NewPath, SysTrustedFormat);
    end;
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\Variables', 'TRUSTEDPATHS', NewPath);
  end else if IsInstall then begin
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\Variables', 'TRUSTEDPATHS', RootTrustedFormat + ';' + SysTrustedFormat);
  end;

  if RegQueryStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'TRUSTEDPATHS', CurrentPath) then
  begin
    if IsInstall then
    begin
      NewPath := AddToPathList(CurrentPath, RootTrustedFormat);
      NewPath := AddToPathList(NewPath, SysTrustedFormat);
    end else begin
      NewPath := RemoveFromPathList(CurrentPath, RootTrustedFormat);
      NewPath := RemoveFromPathList(NewPath, SysTrustedFormat);
    end;
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'TRUSTEDPATHS', NewPath);
  end else if IsInstall then begin
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'TRUSTEDPATHS', RootTrustedFormat + ';' + SysTrustedFormat);
  end;
end;

procedure ProcessAcadInstance(const RKey, InstName, BaseR, InstallRoot: string; IsInstall: Boolean);
var
  AppKey, AppKeyLegacy, AcadInstKey, ProfileKey: string;
  SysR, SysDir, RootTrustedFormat, SysTrustedFormat, DllPath: string;
  ProfileNames: TArrayOfString;
  k: Integer;
begin
  SysR := 'Sys' + Copy(BaseR, 2, Length(BaseR) - 1);
  SysDir := InstallRoot + '\' + SysR;
  RootTrustedFormat := InstallRoot + '\...';
  SysTrustedFormat := SysDir + '\...';

  DllPath := FindDllForBaseR(InstallRoot, BaseR);
  AppKey := RKey + '\' + InstName + '\Applications\CADAutoIme';
  AppKeyLegacy := RKey + '\' + InstName + '\Applications\{#MyAppNameEn}';

  if IsInstall then
  begin
    if (DllPath <> '') and FileExists(DllPath) then
    begin
      RegWriteDWordValue(HKEY_CURRENT_USER, AppKey, 'LOADCTRLS', 2);
      RegWriteDWordValue(HKEY_CURRENT_USER, AppKey, 'MANAGED', 1);
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey, 'LOADER', DllPath);
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey, 'DESCRIPTION', '{#MyAppNameZh}');
    end;
  end else begin
    if RegKeyExists(HKEY_CURRENT_USER, AppKey) then
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, AppKey);
    if RegKeyExists(HKEY_CURRENT_USER, AppKeyLegacy) then
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, AppKeyLegacy);
  end;

  AcadInstKey := RKey + '\' + InstName + '\Profiles';
  if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadInstKey, ProfileNames) then
  begin
    for k := 0 to GetArrayLength(ProfileNames) - 1 do
    begin
      ProfileKey := AcadInstKey + '\' + ProfileNames[k];
      ConfigureAcadProfile(ProfileKey, InstallRoot, SysDir, RootTrustedFormat, SysTrustedFormat, IsInstall);
    end;
  end;
end;

// ==========================================
// 🚀 阶段1：安装前置环境与防死锁检查
// ==========================================
function InitializeSetup(): Boolean;
var
  AcadKey: string;
begin
  Result := True;
  AcadKey := 'Software\Autodesk\AutoCAD';

  if (not RegKeyExists(HKEY_CURRENT_USER, AcadKey)) and (not RegKeyExists(HKEY_LOCAL_MACHINE, AcadKey)) then
  begin
    MsgBox('⛔ 程序安装拦截！' + #13#10#13#10 +
           '系统中未检测到任何 AutoCAD 环境。' + #13#10#13#10 +
           '本程序必须依赖 AutoCAD 运行，请【先安装 AutoCAD 并至少运行过一次】，然后再运行本安装程序！', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  while IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe') do
  begin
    case MsgBox('安装程序检测到 [ AutoCAD ] 正在运行中。' + #13#10#13#10 +
                '为保证插件底层核心组件能够顺利写入并生效，必须先关闭 AutoCAD。' + #13#10#13#10 +
                '▶ 【确定】(是) ：我已手动保存图纸并退出 AutoCAD，继续检查。' + #13#10 +
                '▶ 【强制关闭并继续】(否) ：我不在乎未保存的图纸，直接强制结束 AutoCAD 进程。' + #13#10 +
                '▶ 【取消】  ：放弃并退出安装。', mbConfirmation, MB_YESNOCANCEL) of
      IDYES:
        begin
        end;
      IDNO:
        begin
          ForceKillCAD();
          if not (IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe')) then Break;
        end;
      IDCANCEL:
        begin
          Result := False;
          Exit;
        end;
    end;
  end;
end;

// ==========================================
// 🚀 阶段2：向导初始化 (CAD 全代嗅探与选择)
// ==========================================
procedure InitializeWizard;
var
  AcadKey, RKey, RName, DisplayName, BaseR: string;
  RNames, InstNames: TArrayOfString;
  i: Integer;
begin
  FoundRNames := TStringList.Create;
  AcadVersionPage := CreateInputOptionPage(wpSelectDir,
    '选择挂载版本', '检测到以下兼容的 AutoCAD 版本', '请勾选需要启用【{#MyAppNameZh}】的 AutoCAD 对应版本：', False, False);

  AcadKey := 'Software\Autodesk\AutoCAD';
  if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadKey, RNames) then
  begin
    for i := 0 to GetArrayLength(RNames) - 1 do
    begin
      RName := RNames[i];
      BaseR := GetBaseRFromRName(RName, DisplayName);

      if DisplayName <> '' then
      begin
        RKey := AcadKey + '\' + RName;
        if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
        begin
          if GetArrayLength(InstNames) > 0 then
          begin
            AcadVersionPage.Add(DisplayName + ' [' + RName + ']');
            AcadVersionPage.Values[AcadVersionPage.CheckListBox.Items.Count - 1] := True;
            FoundRNames.Add(RName + '|' + BaseR);
          end;
        end;
      end;
    end;
  end;

  if AcadVersionPage.CheckListBox.Items.Count = 0 then
  begin
     AcadVersionPage.Add('未检测到已配置的 AutoCAD 实例，将默认部署全部核心');
     AcadVersionPage.Values[0] := True;
     FoundRNames.Add('Default|R24');
  end;
end;

// ==========================================
// 🚀 阶段3：写入加载项、信任路径与注册表自启
// ==========================================
procedure CurStepChanged(CurStep: TSetupStep);
var
  AcadKey, RKey, ItemData, RName, BaseR, InstallRoot: string;
  InstNames: TArrayOfString;
  i, j: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    InstallRoot := ExpandConstant('{app}');
    AcadKey := 'Software\Autodesk\AutoCAD';
    RegWriteStringValue(HKEY_CURRENT_USER, 'Software\OpenCadIme', 'InstallDir', InstallRoot);
    RegWriteStringValue(HKEY_CURRENT_USER, 'Software\OpenCadIme\CADAutoIme', 'Version', '{#MyAppVersion}');

    if Assigned(FoundRNames) and Assigned(AcadVersionPage) then
    begin
      for i := 0 to AcadVersionPage.CheckListBox.Items.Count - 1 do
      begin
        if AcadVersionPage.Values[i] and (i < FoundRNames.Count) then
        begin
          ItemData := FoundRNames[i];
          RName := Copy(ItemData, 1, Pos('|', ItemData) - 1);
          BaseR := Copy(ItemData, Pos('|', ItemData) + 1, Length(ItemData));
          
          if RName <> 'Default' then
          begin
            RKey := AcadKey + '\' + RName;
            if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
              for j := 0 to GetArrayLength(InstNames) - 1 do
                ProcessAcadInstance(RKey, InstNames[j], BaseR, InstallRoot, True);
          end;
        end;
      end;
    end;
  end;
end;

// ==========================================
// 🚀 阶段4：卸载清理 (进程检查、路径还原与配置保留)
// ==========================================
function InitializeUninstall(): Boolean;
begin
  Result := True;
  G_DeleteUserData := False;
  if UninstallSilent then Exit;

  while IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe') do
  begin
    case MsgBox('检测到 [ AutoCAD ] 正在运行中。为确保文件彻底清除，请先关闭 AutoCAD。' + #13#10#13#10 +
                '▶ 【确定】(是) ：我已保存图纸并退出 AutoCAD，继续。' + #13#10 +
                '▶ 【强制关闭】(否) ：直接强制结束 AutoCAD 进程（未保存的数据将丢失）。' + #13#10 +
                '▶ 【取消】  ：放弃并退出卸载。', mbConfirmation, MB_YESNOCANCEL) of
      IDYES: begin end;
      IDNO:
        begin
          ForceKillCAD();
          if not (IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe')) then Break;
        end;
      IDCANCEL: begin Result := False; Exit; end;
    end;
  end;

  if MsgBox('即将卸载 {#MyAppNameZh}。' + #13#10#13#10 +
            '是否要彻底清除所有配置数据？' + #13#10#13#10 +
            '(选择【是】将删除自定义命令白名单及错误日志；选择【否】将保留您的命令白名单)', mbConfirmation, MB_YESNO) = IDYES then
  begin
    G_DeleteUserData := True;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AcadKey, RKey, InstallRoot, BaseR, DummyName: string;
  RNames, InstNames: TArrayOfString;
  i, j: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    InstallRoot := ExpandConstant('{app}');
    AcadKey := 'Software\Autodesk\AutoCAD';

    if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadKey, RNames) then
    begin
      for i := 0 to GetArrayLength(RNames) - 1 do
      begin
        BaseR := GetBaseRFromRName(RNames[i], DummyName);
        if BaseR <> '' then
        begin
          RKey := AcadKey + '\' + RNames[i];
          if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
            for j := 0 to GetArrayLength(InstNames) - 1 do
              ProcessAcadInstance(RKey, InstNames[j], BaseR, InstallRoot, False);
        end;
      end;
    end;

    if G_DeleteUserData then
    begin
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\OpenCadIme');
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\QianZuiMoYu\CADAutoIme');
    end;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    InstallRoot := ExpandConstant('{app}');
    if G_DeleteUserData then
    begin
      DelTree(InstallRoot, True, True, True);
      if FileExists(ExpandConstant('{userdocs}\AutoImeCommands.txt')) then
        DeleteFile(ExpandConstant('{userdocs}\AutoImeCommands.txt'));
    end
    else
    begin
      // 仅清理程序文件目录中的 DLL，保留 AutoImeCommands.txt
      for i := 17 to 27 do
      begin
        if DirExists(InstallRoot + '\Sys' + IntToStr(i)) then
          DelTree(InstallRoot + '\Sys' + IntToStr(i), True, True, True);
        if DirExists(InstallRoot + '\R' + IntToStr(i)) then
          DelTree(InstallRoot + '\R' + IntToStr(i), True, True, True);
      end;
      DeleteFile(InstallRoot + '\Readme.html');
      DeleteFile(InstallRoot + '\Icon.ico');
    end;
  end;
end;
