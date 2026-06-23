using System;
using System.Runtime.InteropServices;
using System.Text;
namespace OpenCadIme
{
    internal static class Win32API
    {
        // ==========================================
        // 1. IMM32.dll (输入法核心 API)
        // ==========================================
        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ImmGetContext(IntPtr hwnd);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmSetOpenStatus(IntPtr hIMC, bool b);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmGetOpenStatus(IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr hIMC);

        // --- 终极防御：输入法上下文剥离 API (拔网线技术核心) ---
        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmGetConversionStatus(IntPtr hIMC, out uint lpfdwConversion, out uint lpfdwSentence);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmSetConversionStatus(IntPtr hIMC, uint fdwConversion, uint fdwSentence);
        // ==========================================
        // 2. USER32.dll & KERNEL32 (焦点、穿透与系统级事件钩子 API)
        // ==========================================
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        // --- ⭐透视眼镜 API，用于获取窗口的真实类名 ---
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // --- HUD 界面所需的鼠标穿透与窗口状态 API ---
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // --- 系统级事件监听核心 API ---
        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetCurrentProcessId();
        // ==========================================
        // 3. GDI32.dll (UI 圆角绘制)
        // ==========================================
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn", CharSet = CharSet.Auto)]
        public static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);
        // ==========================================
        // 4. 常量定义 (Constants)
        // ==========================================
        public const uint IME_CMODE_ALPHANUMERIC = 0x0000; // 纯英文模式
        public const uint IME_CMODE_NATIVE = 0x0001;       // 中文/本地化模式
        public const uint IME_CMODE_FULLSHAPE = 0x0008;    // 全角模式
        public const uint IME_CMODE_SYMBOL = 0x0400;       // 标点模式
        public const uint EVENT_OBJECT_FOCUS = 0x8005;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        // HUD 鼠标穿透所需常量
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_LAYERED = 0x80000;
        // ==========================================
        // 5. 结构体定义 (Structs)
        // ==========================================
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; public int Top; public int Right; public int Bottom;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }
    }
}
