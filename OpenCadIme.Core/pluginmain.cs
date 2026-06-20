using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

[assembly: ExtensionApplication(typeof(OpenCadIme.PluginMain))]

namespace OpenCadIme
{
    public class PluginMain : IExtensionApplication, IDisposable
    {
        private const string AppVersion = "0.3 ";

        private Dictionary<string, bool> _whitelistCommands;
        private List<Document> _hookedDocs = new List<Document>();
        private List<Document> _pendingWelcomeDocs = new List<Document>();
        private readonly object _docLock = new object();

        private bool _isPluginEnabled = true;
        private bool _disposed = false;

        private bool _isTextCommandActive = false;

        private HudManager _hudManager;

        private IntPtr _winEventHook = IntPtr.Zero;
        private Win32API.WinEventDelegate _hookDelegate;
        private uint _currentProcessId;
        private IntPtr _lastFocusHwnd = IntPtr.Zero;
        private DateTime _lastFocusTime = DateTime.MinValue;

        private StringBuilder _classNameBuffer = new StringBuilder(256);

        public void Initialize()
        {
            try
            {
                _isPluginEnabled = true;
                _disposed = false;

                _currentProcessId = Win32API.GetCurrentProcessId();
                _hookDelegate = new Win32API.WinEventDelegate(WinEventCallback);
                _winEventHook = Win32API.SetWinEventHook(
                    Win32API.EVENT_OBJECT_FOCUS, Win32API.EVENT_OBJECT_FOCUS,
                    IntPtr.Zero, _hookDelegate, _currentProcessId, 0, Win32API.WINEVENT_OUTOFCONTEXT);

                _hudManager = new HudManager();
                _whitelistCommands = ConfigManager.LoadCommands();

                foreach (Document doc in Application.DocumentManager) AttachEvents(doc);

                Application.DocumentManager.DocumentCreated += OnDocumentCreated;
                Application.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
                Application.DocumentManager.DocumentDestroyed += OnDocumentDestroyed;
                System.Windows.Forms.Application.Idle += OnApplicationIdle;

                ImeController.ForceEnglish(GetCadMainWindowHandle());
            }
            catch (System.Exception ex) { LogError("Initialize", "初始化失败", ex); }
        }

        public void Terminate() { Dispose(); }
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try
                {
                    if (_winEventHook != IntPtr.Zero)
                    {
                        Win32API.UnhookWinEvent(_winEventHook);
                        _winEventHook = IntPtr.Zero;
                    }
                    Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                    Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                    Application.DocumentManager.DocumentDestroyed -= OnDocumentDestroyed;
                    System.Windows.Forms.Application.Idle -= OnApplicationIdle;

                    lock (_docLock)
                    {
                        for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                        {
                            if (_hookedDocs[i] != null && !_hookedDocs[i].IsDisposed) DetachEventsUnsafe(_hookedDocs[i]);
                        }
                        _hookedDocs.Clear();
                        _pendingWelcomeDocs.Clear();
                    }
                    if (_hudManager != null) { _hudManager.Dispose(); _hudManager = null; }
                }
                catch (System.Exception ex) { LogError("Dispose", "销毁资源时发生异常", ex); }
            }
            _disposed = true;
        }

        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null) AttachEvents(e.Document);
        }

        private void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null)
            {
                AttachEvents(e.Document);
                if (_isPluginEnabled)
                {
                    _isTextCommandActive = false;
                    ImeController.ForceEnglish(GetCadMainWindowHandle());
                }
            }
        }

        private void OnDocumentDestroyed(object sender, DocumentDestroyedEventArgs e)
        {
            lock (_docLock)
            {
                for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                {
                    if (_hookedDocs[i].IsDisposed || _hookedDocs[i].Name == e.FileName)
                    {
                        DetachEventsUnsafe(_hookedDocs[i]);
                        _hookedDocs.RemoveAt(i);
                    }
                }
                for (int i = _pendingWelcomeDocs.Count - 1; i >= 0; i--)
                {
                    if (_pendingWelcomeDocs[i].IsDisposed || _pendingWelcomeDocs[i].Name == e.FileName) _pendingWelcomeDocs.RemoveAt(i);
                }
            }
        }

        private void AttachEvents(Document doc)
        {
            if (doc == null || doc.IsDisposed) return;
            lock (_docLock)
            {
                if (!_hookedDocs.Contains(doc))
                {
                    doc.CommandWillStart += OnCommandWillStart;
                    doc.CommandEnded += OnCommandEnded;
                    doc.CommandCancelled += OnCommandEnded;
                    doc.CommandFailed += OnCommandEnded;

                    _hookedDocs.Add(doc);
                    if (!_pendingWelcomeDocs.Contains(doc)) _pendingWelcomeDocs.Add(doc);
                }
            }
        }

        private void DetachEventsUnsafe(Document doc)
        {
            try
            {
                if (doc == null || doc.IsDisposed) return;
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandEnded;
                doc.CommandFailed -= OnCommandEnded;
            }
            catch (System.Exception ex) { LogError("DetachEventsUnsafe", "注销图纸事件失败", ex); }
        }

        #region 稳如泰山的核心判定逻辑
        private void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            if (!_isPluginEnabled) return;
            try
            {
                string globalCmd = e.GlobalCommandName;
                if (!string.IsNullOrEmpty(globalCmd) && globalCmd.StartsWith("'")) return;

                string cmd = globalCmd.Trim().TrimStart('_', '-', '\'', '.').ToUpperInvariant();

                if (_whitelistCommands.ContainsKey(cmd))
                {
                    _isTextCommandActive = true;
                    // ⭐ 终极修复 1：一旦命中白名单（如 TBlkname），立刻强制切中文！
                    // 绝不画蛇添足地去切英文，防止命令被反杀。
                    ImeController.ForceChinese(GetCadMainWindowHandle());
                }
                else
                {
                    _isTextCommandActive = false;
                    ImeController.ForceEnglish(GetCadMainWindowHandle());
                }
            }
            catch (System.Exception ex) { LogError("OnCommandWillStart", "命令启动判定异常", ex); }
        }

        private void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (!_isPluginEnabled) return;
            try
            {
                string globalCmd = e.GlobalCommandName;
                if (!string.IsNullOrEmpty(globalCmd) && globalCmd.StartsWith("'")) return;

                _isTextCommandActive = false;
                ImeController.ForceEnglish(GetCadMainWindowHandle());
            }
            catch (System.Exception ex) { LogError("OnCommandEnded", "命令结束判定异常", ex); }
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_isPluginEnabled) return;
            try
            {
                if (eventType == Win32API.EVENT_OBJECT_FOCUS && hwnd != IntPtr.Zero)
                {
                    IntPtr foregroundWnd = Win32API.GetForegroundWindow();
                    uint foregroundPid;
                    Win32API.GetWindowThreadProcessId(foregroundWnd, out foregroundPid);
                    if (foregroundPid != _currentProcessId) return;

                    if (hwnd == _lastFocusHwnd && (DateTime.Now - _lastFocusTime).TotalMilliseconds < 300) return;

                    _lastFocusHwnd = hwnd;
                    _lastFocusTime = DateTime.Now;

                    if (_isTextCommandActive)
                    {
                        _classNameBuffer.Length = 0;
                        Win32API.GetClassName(hwnd, _classNameBuffer, _classNameBuffer.Capacity);
                        string className = _classNameBuffer.ToString();

                        // ⭐ 终极修复 2：在白名单命令状态下，解除对命令行 (AcConsole) 的拦截！
                        // 保证像 TBlkname 这种直接在底部命令行索要文本的插件能完美切出中文。
                        // 只拦截智能提示弹窗 (AcAutoComp) 和右键菜单 (#32768)。
                        if (className.IndexOf("AcAutoComp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            className == "#32768")
                        {
                            return;
                        }

                        ImeController.ForceChinese(hwnd);
                    }
                    else
                    {
                        ImeController.ForceEnglish(hwnd);
                    }
                }
            }
            catch (System.Exception ex) { LogError("WinEventCallback", "焦点雷达执行异常", ex); }
        }
        #endregion

        #region HUD 欢迎界面与自定义命令
        private void OnApplicationIdle(object sender, EventArgs e)
        {
            try
            {
                Document doc = null;
                try { doc = Application.DocumentManager.MdiActiveDocument; } catch { return; }

                if (doc != null && doc.Editor != null)
                {
                    bool shouldShowWelcome = false;
                    lock (_docLock)
                    {
                        if (_pendingWelcomeDocs.Contains(doc))
                        {
                            _pendingWelcomeDocs.Remove(doc);
                            shouldShowWelcome = true;
                        }
                    }

                    if (shouldShowWelcome)
                    {
                        _hudManager?.ShowWelcomeMessage(doc, AppVersion);
                    }
                }
            }
            catch (System.Exception ex) { LogError("OnApplicationIdle", "HUD 欢迎界面展示异常", ex); }
        }

        [CommandMethod("TOGGLEAUTOIME")]
        public void ToggleAutoImeCommand()
        {
            _isPluginEnabled = !_isPluginEnabled;
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                    doc.Editor.WriteMessage("\n>>> [浅醉·墨语] CAD Auto IME 已 " + (_isPluginEnabled ? "开启" : "关闭") + " <<<\n");
            }
            catch (System.Exception ex) { LogError("ToggleAutoImeCommand", "切换开关异常", ex); }
        }

        [CommandMethod("CUSTOMAUTOIME")]
        public void ShowCustomConfigDialog()
        {
            try
            {
                Document doc = null;
                try { doc = Application.DocumentManager.MdiActiveDocument; } catch { }

                using (ConfigForm form = new ConfigForm())
                {
                    System.Windows.Forms.NativeWindow cadWin = new System.Windows.Forms.NativeWindow();
                    cadWin.AssignHandle(GetCadMainWindowHandle());

                    if (form.ShowDialog(cadWin) == System.Windows.Forms.DialogResult.OK)
                    {
                        _whitelistCommands = ConfigManager.LoadCommands();
                        if (doc != null && doc.Editor != null)
                            doc.Editor.WriteMessage("\n>>> [浅醉·墨语] 配置已更新，当前共加载 " + ConfigManager.LoadedCustomCount + " 个白名单！ <<<\n");
                    }
                }
                if (_isPluginEnabled) ImeController.ForceEnglish(GetCadMainWindowHandle());
            }
            catch (System.Exception ex) { LogError("ShowCustomConfigDialog", "配置面板弹出异常", ex); }
        }

        private static IntPtr GetCadMainWindowHandle()
        {
            try { return Application.MainWindow.Handle; }
            catch { return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
        }

        private void LogError(string methodName, string msg, System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[CADAutoIME-" + methodName + "] " + msg + " \n异常信息: " + ex?.Message + "\n堆栈: " + ex?.StackTrace);
        }
        #endregion
    }
}