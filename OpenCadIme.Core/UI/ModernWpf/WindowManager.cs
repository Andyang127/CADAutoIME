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
                System.Reflection.MethodInfo showModalMethod = typeof(Autodesk.AutoCAD.ApplicationServices.Application).GetMethod("ShowModalWindow", new Type[] { typeof(Window) });
                if (showModalMethod != null)
                {
                    return (bool?)showModalMethod.Invoke(null, new object[] { wpfWindow });
                }
                Type coreAppType = Type.GetType("Autodesk.AutoCAD.ApplicationServices.Core.Application, accoremgd");
                if (coreAppType != null)
                {
                    showModalMethod = coreAppType.GetMethod("ShowModalWindow", new Type[] { typeof(Window) });
                    if (showModalMethod != null)
                    {
                        return (bool?)showModalMethod.Invoke(null, new object[] { wpfWindow });
                    }
                }
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
                System.Windows.Forms.MessageBox.Show($"WPF 窗口管理器加载失败！\n\n原因: {ex.Message}\n\n内部: {ex.InnerException?.Message}", "致命错误 - WindowManager", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }
    }
}