using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// 【新增】：引入常量所在的命名空间
using OpenCadIme;

// 有关程序集的一般信息由以下
// 控制。更改这些特性值可修改
// 与程序集关联的信息。

// 注意：Title 代表 DLL 描述，每个 Sys 项目不一样，保持硬编码
[assembly: AssemblyTitle("CAD 2007-2009.dll")]
[assembly: AssemblyDescription(AppConstants.PluginFullName)]
[assembly: AssemblyConfiguration("")]

// 【优化】：动态引用作者名与产品名
[assembly: AssemblyCompany(AppConstants.AuthorName)]
[assembly: AssemblyProduct(AppConstants.PluginShortName)]
[assembly: AssemblyCopyright("Copyright © " + AppConstants.AuthorName + " 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// 将 ComVisible 设置为 false 会使此程序集中的类型
//对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型
//请将此类型的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 注意：GUID 是 COM 识别码，每个 Sys 项目【必须绝对唯一】，保持硬编码
[assembly: Guid("bc84d7d6-2e62-4841-8d17-4aa4456eee3d")]

// 程序集的版本信息由下列四个值组成: 
//
//      主版本
//      次版本
//      生成号
//      修订号
//
// 【终极杀招】：动态引用全局版本号，并拼凑出四位数的格式 (如 "0.4.0" + ".0" = "0.4.0.0")
[assembly: AssemblyVersion(AppConstants.Version + ".0")]
[assembly: AssemblyFileVersion(AppConstants.Version + ".0")]