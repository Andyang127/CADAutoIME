#pragma warning disable CA1416
using System;
using System.Windows;
using System.Windows.Interop;
using System.Diagnostics;

namespace OpenCadIme.UI.ModernWpf
{
    public static class WindowManager
    {
        public static bool? ShowModal(Window wpfWindow)
        {
            if (wpfWindow == null) return false;

            try
            {
                // 1. 优先尝试 AutoCAD 官方专用的 WPF 挂载方法
                System.Reflection.MethodInfo showModalMethod = typeof(Autodesk.AutoCAD.ApplicationServices.Application).GetMethod("ShowModalWindow", new Type[] { typeof(Window) });
                if (showModalMethod != null)
                {
                    return (bool?)showModalMethod.Invoke(null, new object[] { wpfWindow });
                }

                // 2. 针对极高版本 CAD (Core 核心分离架构) 的探测
                Type coreAppType = Type.GetType("Autodesk.AutoCAD.ApplicationServices.Core.Application, accoremgd");
                if (coreAppType != null)
                {
                    showModalMethod = coreAppType.GetMethod("ShowModalWindow", new Type[] { typeof(Window) });
                    if (showModalMethod != null)
                    {
                        return (bool?)showModalMethod.Invoke(null, new object[] { wpfWindow });
                    }
                }

                // 3. 兜底：使用 Win32 句柄挂载
                IntPtr cadMainWindowHandle = IntPtr.Zero;
                try { cadMainWindowHandle = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle; } catch { }
                if (cadMainWindowHandle == IntPtr.Zero)
                {
                    try { cadMainWindowHandle = Process.GetCurrentProcess().MainWindowHandle; } catch { }
                }

                WindowInteropHelper helper = new WindowInteropHelper(wpfWindow);
                if (cadMainWindowHandle != IntPtr.Zero)
                {
                    helper.Owner = cadMainWindowHandle;
                }

                return wpfWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // 【终极追踪】：如果在挂载 WPF 时发生崩溃，绝对不允许静默失败！
                System.Windows.Forms.MessageBox.Show($"WPF 窗口管理器加载失败！\n\n原因: {ex.Message}\n\n内部: {ex.InnerException?.Message}", "致命错误 - WindowManager", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }
    }
}