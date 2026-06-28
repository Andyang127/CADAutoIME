using System;
using OpenCadIme.Interop;

namespace OpenCadIme.Core
{
    /// <summary>
    /// 焦点事件管理器 (惰性挂载版)
    /// </summary>
    public class FocusHookManager : IDisposable
    {
        private IntPtr _winEventHook = IntPtr.Zero;
        private Win32API.WinEventDelegate _hookDelegate;
        private uint _currentProcessId;
        private bool _disposed = false;

        public IntPtr CurrentFocusHwnd { get; private set; } = IntPtr.Zero;

        public event EventHandler FocusChanged;

        public FocusHookManager()
        {
            _currentProcessId = Win32API.GetCurrentProcessId();
            _hookDelegate = new Win32API.WinEventDelegate(WinEventCallback);
            // 初始化时保持绝对静默，不挂载钩子
        }

        /// <summary>
        /// 按需唤醒：仅在白名单命令执行时调用
        /// </summary>
        public void StartListening()
        {
            if (_disposed || _winEventHook != IntPtr.Zero) return;
            try
            {
                _winEventHook = Win32API.SetWinEventHook(
                    Win32API.EVENT_OBJECT_FOCUS,
                    Win32API.EVENT_OBJECT_FOCUS,
                    IntPtr.Zero,
                    _hookDelegate,
                    _currentProcessId,
                    0,
                    Win32API.WINEVENT_OUTOFCONTEXT);
            }
            catch (Exception ex)
            {
                Logger.Error("FocusHookManager", "挂载惰性钩子失败", ex);
            }
        }

        /// <summary>
        /// 阅后即焚：命令结束后立即调用，释放资源
        /// </summary>
        public void StopListening()
        {
            if (_winEventHook != IntPtr.Zero)
            {
                Win32API.UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
                CurrentFocusHwnd = IntPtr.Zero; // 清理状态缓存
            }
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (eventType != Win32API.EVENT_OBJECT_FOCUS || hwnd == IntPtr.Zero)
                {
                    return;
                }

                uint hwndPid;
                Win32API.GetWindowThreadProcessId(hwnd, out hwndPid);
                if (hwndPid != _currentProcessId || hwnd == CurrentFocusHwnd)
                {
                    return;
                }

                CurrentFocusHwnd = hwnd;
                FocusChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error("FocusHookManager", "焦点回调执行引发异常", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopListening();
            _disposed = true;
        }
    }
}