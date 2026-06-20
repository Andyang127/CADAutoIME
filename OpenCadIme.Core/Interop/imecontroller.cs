using System;
using System.Runtime.InteropServices;

namespace OpenCadIme
{
    /// <summary>
    /// 输入法控制器 
    /// </summary>
    internal static class ImeController
    {
        public static void ForceEnglish(IntPtr cadMainWindowHwnd)
        {
            try
            {
                IntPtr targetHwnd = GetRealFocusWindow(cadMainWindowHwnd);
                if (targetHwnd == IntPtr.Zero) targetHwnd = cadMainWindowHwnd;

                IntPtr activeHimc = Win32API.ImmGetContext(targetHwnd);
                if (activeHimc != IntPtr.Zero)
                {
                    // 1. 温和地关闭输入法 (相当于按了 Shift)
                    if (Win32API.ImmGetOpenStatus(activeHimc))
                    {
                        Win32API.ImmSetOpenStatus(activeHimc, false);
                    }

                    // 2. 强制锁入纯英文模式
                    uint conversion, sentence;
                    if (Win32API.ImmGetConversionStatus(activeHimc, out conversion, out sentence))
                    {
                        uint newConversion = Win32API.IME_CMODE_ALPHANUMERIC;
                        if (conversion != newConversion)
                        {
                            Win32API.ImmSetConversionStatus(activeHimc, newConversion, sentence);
                        }
                    }
                    Win32API.ImmReleaseContext(targetHwnd, activeHimc);
                }
            }
            catch (Exception ex)
            {
                // 将底层的屏蔽异常暴露出来，能在 VS 的“输出”窗口中直接看到
                System.Diagnostics.Debug.WriteLine("[CADAutoIME-ImeController] ForceEnglish 执行异常: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        public static void ForceChinese(IntPtr cadMainWindowHwnd)
        {
            try
            {
                IntPtr targetHwnd = GetRealFocusWindow(cadMainWindowHwnd);
                if (targetHwnd == IntPtr.Zero) targetHwnd = cadMainWindowHwnd;

                IntPtr activeHimc = Win32API.ImmGetContext(targetHwnd);
                if (activeHimc != IntPtr.Zero)
                {
                    // 1. 打开输入法
                    if (!Win32API.ImmGetOpenStatus(activeHimc))
                    {
                        Win32API.ImmSetOpenStatus(activeHimc, true);
                    }

                    // 2. 清洗全角，强注中文标点和本地模式
                    uint conversion, sentence;
                    if (Win32API.ImmGetConversionStatus(activeHimc, out conversion, out sentence))
                    {
                        uint targetMode = Win32API.IME_CMODE_NATIVE | Win32API.IME_CMODE_SYMBOL;
                        uint newConversion = (conversion | targetMode) & ~Win32API.IME_CMODE_FULLSHAPE;

                        if (conversion != newConversion)
                        {
                            Win32API.ImmSetConversionStatus(activeHimc, newConversion, sentence);
                        }
                    }
                    Win32API.ImmReleaseContext(targetHwnd, activeHimc);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CADAutoIME-ImeController] ForceChinese 执行异常: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private static IntPtr GetRealFocusWindow(IntPtr fallbackHwnd)
        {
            try
            {
                IntPtr foregroundWindow = Win32API.GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    uint dummyPid;
                    uint threadId = Win32API.GetWindowThreadProcessId(foregroundWindow, out dummyPid);
                    Win32API.GUITHREADINFO guiInfo = new Win32API.GUITHREADINFO();
                    guiInfo.cbSize = Marshal.SizeOf(guiInfo);

                    if (Win32API.GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndFocus != IntPtr.Zero)
                    {
                        return guiInfo.hwndFocus;
                    }
                }
                IntPtr focus = Win32API.GetFocus();
                if (focus != IntPtr.Zero) return focus;

                return fallbackHwnd;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CADAutoIME-ImeController] GetRealFocusWindow 获取焦点异常: " + ex.Message);
                return fallbackHwnd;
            }
        }
    }
}