# CAD Auto IME

AutoCAD 输入法自动切换插件。根据命令类型自动切换中英文输入法状态，减少手动切换的操作中断。

**版本**: v0.3.1 | **命名空间**: `OpenCadIme`

---

## 功能特性

- **自动切换**：执行文字类命令时自动切中文，执行绘图类命令时自动切英文
- **双引擎判定**：结合命令事件监听与窗口焦点监测，降低误切换概率
- **多文档隔离**：每张图纸维护独立的输入法状态，切换文档互不干扰（v0.3.1 新增）
- **白名单配置**：支持自定义添加命令，图形化管理界面
- **开箱即用**：内置 90+ 常用命令，覆盖 CAD 原生及主流第三方插件
- **轻量运行**：内存复用 + 防抖优化，对 CAD 性能影响极小

---

## 快速开始

### 安装

1. 关闭所有 AutoCAD 窗口
2. 运行安装程序，完成注册表注入与 Bundle 配置
3. 启动 AutoCAD，底部出现欢迎悬浮窗即为加载成功


>
> ⚠️ 安装前请确保 AutoCAD 已至少运行过一次（完成首次初始化）。若后续重装或升级 AutoCAD，请重新安装本插件。
>

### 基本使用

安装后无需额外配置即可使用。插件会自动识别当前执行的命令类型，切换对应的输入法状态。

常用控制命令：


| 命令          | 功能                                |
|-----------------|---------------------------------------|
| `CUSTOMAUTOIME` | 打开白名单配置面板           |
| `TOGGLEAUTOIME` | 一键开启/关闭自动切换功能 |


---

## 白名单配置指南

### 配置面板

在 CAD 命令行输入 `CUSTOMAUTOIME` 打开配置面板，支持以下操作：

- **添加命令**：输入命令全称，回车或点击添加
- **批量添加**：支持空格、逗号、分号分隔的批量输入
- **删除命令**：双击条目或选中后按 Delete 键
- **搜索过滤**：顶部搜索框实时过滤
- **撤销/重做**：Ctrl+Z 撤销，Ctrl+Y 重做
- **恢复默认**：一键恢复内置默认命令

>
> ⚠️ **重要**：添加命令时必须输入**命令全称**（Full Command Name），不能使用快捷键（Alias）。
> 例如：创建块应输入 `BLOCK`，不能输入 `B`；画圆应输入 `CIRCLE`，不能输入 `C`。
>

### 配置文件位置

配置文件存储于：

```
%AppData%\OpenCadIme\AutoImeCommands.txt
```

配置文件为纯文本格式，每行一个命令，支持
`//`开头的注释行。也可以直接编辑该文件，重启 CAD 后生效。

---

---

## **CAD Auto IME 启动界面**

<p align="center"><img width="80%" alt="CAD Auto IME 主界面" src="https://github.com/user-attachments/assets/ac75f8b2-43fe-49c9-bb6d-95daab82c735" /></p>

## **CUSTOMAUTOIME 配置面板**

<p align="center"><img width="50%" alt="CAD Auto IME 配置面板" src="https://github.com/user-attachments/assets/569a0fd1-546d-42e5-87d5-b0cfed172cbb" /></p>

---

## 内置命令清单

### CAD 原生系统命令（45 个）

**文字编辑**`TEXT` `DTEXT` `MTEXT` `-MTEXT` `MTEDIT` `DDEDIT` `FIND`

**属性定义**`ATTDEF` `-ATTDEF` `ATTEDIT` `-ATTEDIT` `EATTEDIT` `BATTMAN` `ATTREDEF`

**引线标注**`QLEADER` `LEADER` `MLEADER` `MLEADERCONTENTEDIT`

**表格操作**`TABLE` `TABLEDIT` `TOBJEDIT`

**文件操作**`SAVEAS` `EXPORT` `WBLOCK` `TABLEEXPORT` `OPEN` `NEW` `PUBLISH` `SAVE` `QSAVE`

**块与样式**`BLOCK` `-BLOCK` `BMAKE` `RENAME` `-RENAME` `STYLE` `LAYER` `-LAYER` `DIMSTYLE` `GROUP`

**其他**`PLOT` `PAGESETUP` `QSELECT` `FILTER` `HATCH` `BHATCH`

### 第三方插件命令（50+ 个）

**天正建筑/结构/机电**`DHWZ` `TMBZ` `ZFBZ` `YCBZ` `SYTM` `FJMC` `WDNAM` `GJMC` `JSBZ` `ZWBZ` `SMBZ` `TBLKNAME` `TKGM` `GGWZ` `WZYS` `QXWZ`

**探索者 TSSD（结构）**`TS_SMZ` `TS_GJMC` `TS_JJS` `TS_HFBZ`

**源泉设计 YQArch**`YQ_WZPL` `YQ_BZBJ` `YQ_BZPL` `YQ_YPZ` `YQ_MCBZ` `YQ_GJMC`

**海龙工具箱（室内设计）**`DD` `AF` `AT` `AB` `ABB`

**常青藤辅助工具**`IVT_TextEdit` `IVT_TextSerial` `IVT_AttEdit` `IVT_BlockRename`

**燕秀工具箱（机械模具）**`YX_CK` `YX_MJ` `YX_WT`

**通用 LISP / 全插件通用**`DHSHR` `DHBJ` `WZSHR` `TYBJ` `WZPL` `BZPL` `YPZ` `XXBZH` `PMZ` `LMBZ` `PMMZ` `SBMC` `SMWZ`

---

## 工作原理

### 双引擎判定机制

插件采用两种机制结合的方式判断当前是否需要切换为中文：

1. **命令事件引擎**：监听 `CommandWillStart` 和 `CommandEnded` 事件，当执行白名单内的文字命令时，标记"文本命令激活"状态
2. **焦点雷达引擎**：通过 `SetWinEventHook` 监听 `EVENT_OBJECT_FOCUS` 事件，实时捕获当前焦点窗口的类名


两种机制互补：

- 命令事件引擎负责"什么时候该切中文"
- 焦点雷达引擎负责"什么时候该切回英文"（如右键菜单、智能提示弹窗抢焦点时保持英文）

### 多文档状态隔离（v0.3.1）

v0.3.1 版本将全局文本命令状态重构为按 `Document` 对象维度存储：

```csharp
Dictionary<Document, bool> _textCommandActiveDocs
```

每个文档维护独立的输入法状态机，切换文档时自动重置，避免多图纸作业时的状态串扰。

### 输入法控制

通过 Win32 API 直接操作输入法句柄（HIMC）：

- `ImmGetContext` / `ImmReleaseContext`：获取/释放输入法上下文
- `ImmSetConversionStatus`：设置输入法转换模式
- `ImmAssociateContext`：关联/脱离输入法句柄

---

## 兼容性说明

### AutoCAD 版本


| 版本范围        | .NET 框架    | 状态    |
|---------------------|----------------|-----------|
| AutoCAD 2007 ~ 2014 | .NET 2.0 ~ 4.0 | 支持    |
| AutoCAD 2015 ~ 2024 | .NET 4.x       | 支持    |
| AutoCAD 2025        | .NET 8.0       | 已适配 |
| AutoCAD 2026        | .NET 9.0       | 已适配 |
| AutoCAD 2027        | .NET 10.0      | 已适配 |


### 输入法

- 微软拼音（系统自带）
- 微信输入法
- 小狼毫（Rime）
- 搜狗拼音
- 百度输入法
- 其他兼容 TSF 框架的中文输入法

### 第三方插件

已测试兼容的主流插件：

- 天正建筑/结构/机电
- 探索者 TSSD
- 源泉设计 YQArch
- 海龙工具箱
- 常青藤辅助工具
- 燕秀工具箱

>
> 说明：以上为测试过的插件，其他 LISP 或 .NET 插件理论上也可兼容，如遇不支持的命令可手动添加至白名单。
>

---

## 常见问题

### Q1：为什么有些命令不自动切中文？

**A**：请检查以下几点：

1. 确认输入的是命令全称，不是快捷键
2. 确认该命令已添加到白名单中
3. 部分插件的命令可能通过其他方式触发（如菜单、按钮），可能无法被命令事件捕获


### Q2：可以删除 TEXT、MTEXT 等系统内置命令吗？

**A**：不建议删除。这些是
AutoCAD文字录入的核心命令，删除后对应命令将不再自动切换输入法。如误删，可通过配置面板的「恢复默认」功能重置。

### Q3：同时打开多张图纸会互相影响吗？

**A**：v0.3.1版本已实现多文档状态隔离。每张图纸拥有独立的输入法状态机，切换文档时自动重置，互不干扰。

### Q4：插件加载失败怎么办？

**A**：请检查：

1. AutoCAD 版本是否在支持范围内
2. 插件目录是否已添加到 `TRUSTEDPATHS` 信任路径
3. 尝试以管理员身份运行 AutoCAD
4. 如仍无法加载，卸载后重新安装


### Q5：插件会影响 CAD 运行速度吗？

**A**：本插件采用内存复用和防抖优化，正常使用场景下对 CAD 性能的影响极小。

---

## 开发与构建

>
> 以下内容面向希望参与本项目开发或自行编译的开发者。
>

### 环境要求

- Visual Studio 2022+
- .NET Framework 2.0 ~~ 4.8 / .NET 8.0 ~~ 10.0（视目标 CAD 版本而定）

### 项目结构

```
OpenCadIme/    
├── src/    
│   ├── OpenCadIme.Core/          # 共享核心项目 (.shproj)    
│   │   ├── AppConstants.cs       # 全局常量    
│   │   ├── Win32API.cs           # Win32 P/Invoke 封装    
│   │   ├── ImeController.cs      # 输入法控制器    
│   │   ├── ConfigManager.cs      # 配置文件管理    
│   │   ├── PluginMain.cs         # 插件主入口    
│   │   ├── HudManager.cs         # HUD 欢迎悬浮窗    
│   │   └── ConfigForm.cs         # 配置面板 UI    
│   ├── OpenCadIme.Sys17/         # AutoCAD 2007-2009 目标项目    
│   ├── OpenCadIme.Sys18/         # AutoCAD 2010-2012 目标项目    
│   ├── ...    
│   └── OpenCadIme.Sys27/         # AutoCAD 2027 目标项目    
└── lib/                          # CAD 依赖库（需自行置入）
```

### 构建步骤

1. 克隆仓库
2. 在 `lib/` 目录下对应版本文件夹中置入 CAD 程序集（`acmgd.dll`, `acdbmgd.dll`, `accoremgd.dll`）
3. Visual Studio 中切换至 Release 模式
4. 选择目标版本项目，生成解决方案


### 核心类说明


| 类名          | 职责                                            |
|-----------------|---------------------------------------------------|
| `PluginMain`    | 插件主入口，实现 `IExtensionApplication`  |
| `ImeController` | 输入法控制器，ForceEnglish / ForceChinese  |
| `ConfigManager` | 配置文件读写，原子写入 + 自愈机制  |
| `HudManager`    | HUD 欢迎悬浮窗管理                         |
| `ConfigForm`    | 白名单配置面板（暗黑主题 WinForms）  |
| `Win32API`      | Win32 P/Invoke 封装（IMM32 / User32 / GDI32） |
| `AppConstants`  | 全局共享常量                                |


---

## 版本历史

### v0.3.1（2026-06-23）- 稳定性增强

- **多文档状态隔离**：重构为 `Dictionary<Document, bool>` 按文档维度存储状态，解决多图纸作业时的状态串扰
- **线程安全加固**：WinEvent 回调缓冲区从类级字段改为局部变量，消除并发竞态条件
- **工程化改进**：新增 `AppConstants` 统一管理版本号、配置路径、注册表路径等全局常量
- **细节优化**：HUD 定时器降频（30ms → 100ms）、更新检测增加 5 秒超时、配置文件新增版本标记

### v0.3 - 架构升级

- **双引擎状态锁**：结合命令事件与焦点雷达，降低 PL/BO 等快捷菜单的误切换概率
- **性能优化**：内存 Buffer 复用，减少 GC 频率
- **UI 升级**：全透明双区配置面板，Toast 状态提示
- **配置自愈**：启动时自动检测并补齐缺失的核心命令

### v0.2.2 - 数据流双引擎

- 引入 `ImmAssociateContext` API 优化输入法控制
- 完善多文档生命周期管理

### v0.2.1 / v0.2.0 - 热修复与重构

- 优化输入法状态重置逻辑
- 新增图形化配置面板与 HUD 欢迎界面

### v0.1.0 - 初始版本

- 实现基础的输入法自动切换功能

---

## 免责声明

1. 本软件为免费开源工具，按"现状"提供，不提供任何形式的明示或暗示担保。
2. 使用本软件产生的任何直接或间接损失，作者不承担责任。
3. 请在使用前保存好工作成果，定期备份重要文件。
4. 本软件仅用于学习和交流。


---

**作者**：浅醉·墨语（Andy_127）

**性质**：个人兴趣项目，开源分享

*文档版本: v0.3.1 | 更新时间: 2026-06-23*

 
