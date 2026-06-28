#pragma warning disable CA1416
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenCadIme.Interop
{
    internal static class Win32API
    {
        #region 1. IMM32.dll (传统输入法核心 API)
        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ImmGetContext(IntPtr hwnd);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmSetOpenStatus(IntPtr hIMC, bool b);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmGetOpenStatus(IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmGetConversionStatus(IntPtr hIMC, out uint lpfdwConversion, out uint lpfdwSentence);

        [DllImport("imm32.dll", CharSet = CharSet.Auto)]
        public static extern bool ImmSetConversionStatus(IntPtr hIMC, uint fdwConversion, uint fdwSentence);
        #endregion

        #region 2. USER32.dll & KERNEL32 (焦点、穿透与系统级事件钩子 API)
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        /// <summary>
        /// 获取窗口信息（32/64 位自适应版）
        /// </summary>
        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, nIndex);
            else return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        /// <summary>
        /// 设置窗口信息（32/64 位自适应版）
        /// </summary>
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        /// <summary>
        /// 兼容封装：获取窗口整数值（如扩展样式），内部自动适配 32/64 位
        /// </summary>
        public static int GetWindowLong(IntPtr hWnd, int nIndex)
        {
            return GetWindowLongPtr(hWnd, nIndex).ToInt32();
        }

        /// <summary>
        /// 兼容封装：设置窗口整数值（如扩展样式），内部自动适配 32/64 位
        /// </summary>
        public static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
        {
            return SetWindowLongPtr(hWnd, nIndex, new IntPtr(dwNewLong)).ToInt32();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetCurrentProcessId();
        #endregion

        #region 3. GDI32.dll (UI 圆角绘制)
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn", CharSet = CharSet.Auto)]
        public static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);
        #endregion

        #region 4. 常量定义与结构体
        public const uint IME_CMODE_ALPHANUMERIC = 0x0000;
        public const uint IME_CMODE_NATIVE = 0x0001;
        public const uint IME_CMODE_FULLSHAPE = 0x0008;
        public const uint IME_CMODE_SYMBOL = 0x0400;

        public const uint EVENT_OBJECT_FOCUS = 0x8005;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_LAYERED = 0x80000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
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
        #endregion

        #region 5. 现代 TSF (Text Services Framework) COM 接口与结构体
        [DllImport("msctf.dll")]
        public static extern int TF_CreateThreadMgr(out IntPtr ppTim);

        public static readonly Guid CLSID_TF_ThreadMgr = new Guid("52888644-AB49-11D0-B614-00C04F7895E5");
        public static readonly Guid IID_ITfThreadMgr = new Guid("aa80e801-2021-11d2-93e0-0060b067b86e");
        public static readonly Guid IID_ITfCompartmentMgr = new Guid("7dcf2a8d-75a1-4775-ab84-984dd322a3c7");
        public static readonly Guid GUID_COMPARTMENT_KEYBOARD_OPENCLOSE = new Guid("BA5EA910-E27F-11D3-8AF2-00C04F7546A3");
        public static readonly Guid GUID_COMPARTMENT_KEYBOARD_CONVERSIONSTATUS = new Guid("4a3ad7d4-28a2-4c13-8b80-ad27a2245d7d");

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct Variant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public int lVal;
        }

        [ComImport]
        [Guid("7dcf2a8d-75a1-4775-ab84-984dd322a3c7")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITfCompartmentMgr
        {
            void GetCompartment([In] ref Guid rguid, out ITfCompartment ppComp);
            void ClearCompartment(uint ec, [In] ref Guid rguid);
            void EnumCompartments(out IntPtr ppEnum);
        }

        [ComImport]
        [Guid("bb08c0ef-607e-43c7-9a64-cd2944ce4004")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITfCompartment
        {
            void SetValue(uint ec, [In] ref Variant pvar);
            void GetValue(out Variant pvar);
        }
        #endregion
    }
}