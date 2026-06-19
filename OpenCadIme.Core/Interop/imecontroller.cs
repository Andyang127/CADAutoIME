using System;
using System.Runtime.InteropServices;

namespace OpenCadIme
{
    /// <summary>
    /// 输入法控制器
    /// 封装焦点穿透与“查-切-验”多重防御逻辑，保证绝对的安全与稳定
    /// </summary>
    internal static class ImeController
    {
        public static void ForceEnglish(IntPtr cadMainWindowHwnd)
        {
            SetImeStatus(false, cadMainWindowHwnd);
        }

        public static void ForceChinese(IntPtr cadMainWindowHwnd)
        {
            SetImeStatus(true, cadMainWindowHwnd);
        }

        /// <summary>
        /// 核心：执行安全的“查-切-验”逻辑 (V0.3 包含电源开关与中英文模式双重切换)
        /// </summary>
        private static void SetImeStatus(bool targetStatus, IntPtr fallbackHwnd)
        {
            try
            {
                IntPtr targetHwnd = GetRealFocusWindow(fallbackHwnd);
                if (targetHwnd == IntPtr.Zero) return;

                IntPtr himc = Win32API.ImmGetContext(targetHwnd);
                if (himc == IntPtr.Zero && fallbackHwnd != IntPtr.Zero)
                {
                    targetHwnd = fallbackHwnd;
                    himc = Win32API.ImmGetContext(targetHwnd);
                }

                bool isSwitchSuccess = false;

                // 1. 常规 API 尝试：模拟开关 + Shift 双重控制
                if (himc != IntPtr.Zero)
                {
                    bool currentOpen = Win32API.ImmGetOpenStatus(himc);
                    uint conversion, sentence;
                    bool hasConversion = Win32API.ImmGetConversionStatus(himc, out conversion, out sentence);

                    if (targetStatus) // ======== 目标：切换为中文 ========
                    {
                        if (!currentOpen) Win32API.ImmSetOpenStatus(himc, true);
                        
                        if (hasConversion)
                        {
                            // 强制叠加 Native (中文) 标志：完美模拟按下 Shift 切到中文
                            if ((conversion & Win32API.IME_CMODE_NATIVE) == 0)
                            {
                                Win32API.ImmSetConversionStatus(himc, conversion | Win32API.IME_CMODE_NATIVE, sentence);
                            }
                        }
                        isSwitchSuccess = true;
                    }
                    else // ======== 目标：切换为英文 ========
                    {
                        if (hasConversion)
                        {
                            // 强制剥离 Native 标志，转为 Alphanumeric (英文)：完美模拟按下 Shift 切到英文
                            if ((conversion & Win32API.IME_CMODE_NATIVE) != 0)
                            {
                                Win32API.ImmSetConversionStatus(himc, conversion & ~Win32API.IME_CMODE_NATIVE, sentence);
                            }
                        }
                        
                        // 模式切完后，再把总开关关掉，实现双重保险
                        if (currentOpen) Win32API.ImmSetOpenStatus(himc, false);
                        
                        isSwitchSuccess = true;
                    }

                    // 验证电源状态是否达到预期，未达到则标记为失败交由降维打击处理
                    if (Win32API.ImmGetOpenStatus(himc) != targetStatus) isSwitchSuccess = false;

                    // 必须释放上下文防止 GDI 泄露
                    Win32API.ImmReleaseContext(targetHwnd, himc);
                }

                // 2. 【终极降维打击】(专治 Windows 10 TSF 拦截及各种诡异第三方输入法)
                if (!isSwitchSuccess)
                {
                    // 核心修复：向默认输入法窗口发消息，而不是向 CAD 控件本身发
                    IntPtr imeWnd = Win32API.ImmGetDefaultIMEWnd(targetHwnd);
                    if (imeWnd == IntPtr.Zero && fallbackHwnd != IntPtr.Zero)
                    {
                        imeWnd = Win32API.ImmGetDefaultIMEWnd(fallbackHwnd);
                    }

                    if (imeWnd != IntPtr.Zero)
                    {
                        IntPtr statusPtr = targetStatus ? new IntPtr(1) : IntPtr.Zero;
                        Win32API.SendMessage(imeWnd, Win32API.WM_IME_CONTROL, new IntPtr(Win32API.IMC_SETOPENSTATUS), statusPtr);
                    }
                }
            }
            catch
            {
                // 静默拦截异常，绝不向外抛出引发 AutoCAD 闪退
            }
        }

        private static IntPtr GetRealFocusWindow(IntPtr fallbackHwnd)
        {
            try
            {
                IntPtr foregroundWindow = Win32API.GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    uint threadId = Win32API.GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
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
            catch
            {
                return fallbackHwnd;
            }
        }
    }
}