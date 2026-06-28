#pragma warning disable CA1416
using OpenCadIme.Core;
using System;
using System.Runtime.InteropServices;
using OpenCadIme.Interop;

namespace OpenCadIme
{
    internal static class ImeController
    {
        private static object _tsfThreadMgrObj;

        public static void Initialize()
        {
            if (_tsfThreadMgrObj != null) return;
            try
            {
                // 【核心修复】：防呆设计。强制校验 TSF 组件必须在主线程 (STA) 环境下初始化。
                if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
                {
                    Logger.Error("ImeController", "TSF 引擎初始化被拒绝：必须在主线程 (STA) 中执行！");
                    return;
                }

                Type threadMgrType = Type.GetTypeFromCLSID(Win32API.CLSID_TF_ThreadMgr);
                if (threadMgrType != null)
                {
                    _tsfThreadMgrObj = Activator.CreateInstance(threadMgrType);
                    Logger.Info("ImeController", "TSF 现代双轨引擎初始化成功。");
                }
            }
            catch (Exception ex) { Logger.Error("ImeController", "TSF 引擎初始化失败", ex); }
        }

        private static bool IsAlreadyInState(IntPtr hwnd, bool targetIsChinese)
        {
            try
            {
                IntPtr activeHimc = Win32API.ImmGetContext(hwnd);
                if (activeHimc != IntPtr.Zero)
                {
                    bool isOpen = Win32API.ImmGetOpenStatus(activeHimc);
                    Win32API.ImmReleaseContext(hwnd, activeHimc);
                    return isOpen == targetIsChinese;
                }
            }
            catch { }
            return false;
        }

        public static void ForceEnglish(IntPtr cadMainWindowHwnd)
        {
            IntPtr targetHwnd = IntPtr.Zero;
            IntPtr activeHimc = IntPtr.Zero;
            try
            {
                targetHwnd = GetRealFocusWindow(cadMainWindowHwnd);
                if (targetHwnd == IntPtr.Zero) targetHwnd = cadMainWindowHwnd;

                if (IsAlreadyInState(targetHwnd, false)) return;

                activeHimc = Win32API.ImmGetContext(targetHwnd);
                if (activeHimc != IntPtr.Zero)
                {
                    if (Win32API.ImmGetOpenStatus(activeHimc))
                        Win32API.ImmSetOpenStatus(activeHimc, false);

                    uint conversion, sentence;
                    if (Win32API.ImmGetConversionStatus(activeHimc, out conversion, out sentence))
                    {
                        uint newConversion = Win32API.IME_CMODE_ALPHANUMERIC;
                        if (conversion != newConversion)
                            Win32API.ImmSetConversionStatus(activeHimc, newConversion, sentence);
                    }
                }

                SetTsfCompartmentState(isOpen: false, isChinese: false);
            }
            catch (Exception ex) { Logger.Error("ImeController", "ForceEnglish 执行异常", ex); }
            finally
            {
                if (activeHimc != IntPtr.Zero && targetHwnd != IntPtr.Zero)
                {
                    try { Win32API.ImmReleaseContext(targetHwnd, activeHimc); } catch { }
                }
            }
        }

        public static void ForceChinese(IntPtr cadMainWindowHwnd)
        {
            IntPtr targetHwnd = IntPtr.Zero;
            IntPtr activeHimc = IntPtr.Zero;
            try
            {
                targetHwnd = GetRealFocusWindow(cadMainWindowHwnd);
                if (targetHwnd == IntPtr.Zero) targetHwnd = cadMainWindowHwnd;

                if (IsAlreadyInState(targetHwnd, true)) return;

                activeHimc = Win32API.ImmGetContext(targetHwnd);
                if (activeHimc != IntPtr.Zero)
                {
                    if (!Win32API.ImmGetOpenStatus(activeHimc))
                        Win32API.ImmSetOpenStatus(activeHimc, true);

                    uint conversion, sentence;
                    if (Win32API.ImmGetConversionStatus(activeHimc, out conversion, out sentence))
                    {
                        uint targetMode = Win32API.IME_CMODE_NATIVE | Win32API.IME_CMODE_SYMBOL;
                        if (conversion != targetMode)
                            Win32API.ImmSetConversionStatus(activeHimc, targetMode, sentence);
                    }
                }

                SetTsfCompartmentState(isOpen: true, isChinese: true);
            }
            catch (Exception ex) { Logger.Error("ImeController", "ForceChinese 执行异常", ex); }
            finally
            {
                if (activeHimc != IntPtr.Zero && targetHwnd != IntPtr.Zero)
                {
                    try { Win32API.ImmReleaseContext(targetHwnd, activeHimc); } catch { }
                }
            }
        }

        private static void SetTsfCompartmentState(bool isOpen, bool isChinese)
        {
            if (_tsfThreadMgrObj == null) return;
            try
            {
                Win32API.ITfCompartmentMgr compMgr = _tsfThreadMgrObj as Win32API.ITfCompartmentMgr;
                if (compMgr == null) return;

                Win32API.ITfCompartment openComp = null;
                try
                {
                    Guid openGuid = Win32API.GUID_COMPARTMENT_KEYBOARD_OPENCLOSE;
                    compMgr.GetCompartment(ref openGuid, out openComp);
                    if (openComp != null)
                    {
                        Win32API.Variant varOpen = new Win32API.Variant { vt = 3, lVal = isOpen ? 1 : 0 };
                        openComp.SetValue(0, ref varOpen);
                    }
                }
                finally
                {
                    if (openComp != null && Marshal.IsComObject(openComp))
                        Marshal.ReleaseComObject(openComp);
                }

                Win32API.ITfCompartment convComp = null;
                try
                {
                    Guid convGuid = Win32API.GUID_COMPARTMENT_KEYBOARD_CONVERSIONSTATUS;
                    compMgr.GetCompartment(ref convGuid, out convComp);
                    if (convComp != null)
                    {
                        Win32API.Variant varConv = new Win32API.Variant { vt = 3, lVal = isChinese ? 1 : 0 };
                        convComp.SetValue(0, ref varConv);
                    }
                }
                finally
                {
                    if (convComp != null && Marshal.IsComObject(convComp))
                        Marshal.ReleaseComObject(convComp);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CADAutoIME-ImeController] TSF 隔间控制异常: " + ex.Message);
            }
        }

        private static IntPtr GetRealFocusWindow(IntPtr fallbackHwnd)
        {
            try
            {
                IntPtr focus = Win32API.GetFocus();
                if (focus != IntPtr.Zero) return focus;

                IntPtr foregroundWindow = Win32API.GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    uint dummyPid;
                    uint threadId = Win32API.GetWindowThreadProcessId(foregroundWindow, out dummyPid);
                    Win32API.GUITHREADINFO guiInfo = new Win32API.GUITHREADINFO();
                    guiInfo.cbSize = Marshal.SizeOf(guiInfo);

                    if (Win32API.GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndFocus != IntPtr.Zero)
                        return guiInfo.hwndFocus;
                }
                return fallbackHwnd;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CADAutoIME-ImeController] GetRealFocusWindow 异常: " + ex.Message);
                return fallbackHwnd;
            }
        }

        public static void DisposeTsfEngine()
        {
            if (_tsfThreadMgrObj != null)
            {
                try
                {
                    if (Marshal.IsComObject(_tsfThreadMgrObj))
                        Marshal.ReleaseComObject(_tsfThreadMgrObj);
                    _tsfThreadMgrObj = null;
                    Logger.Info("ImeController", "TSF 非托管引擎资源成功释放。");
                }
                catch (Exception ex) { Logger.Error("ImeController", "释放 TSF 引擎失败", ex); }
            }
        }
    }
}