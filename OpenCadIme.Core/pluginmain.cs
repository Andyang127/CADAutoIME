using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

[assembly: ExtensionApplication(typeof(OpenCadIme.PluginMain))]

namespace OpenCadIme
{
    /// <summary>
    /// 插件的入口点与生命周期总控中心
    /// 负责 AutoCAD 图纸事件绑定，并将切换任务委托给 ImeController
    /// </summary>
    public class PluginMain : IExtensionApplication, IDisposable
    {
        private const string AppVersion = "0.2.2";

        private Dictionary<string, bool> _chineseCommands;
        private List<Document> _hookedDocs = new List<Document>();
        private List<Document> _pendingWelcomeDocs = new List<Document>();

        private readonly object _docLock = new object();

        private bool _isPluginEnabled = true;
        private bool _disposed = false;

        private HudManager _hudManager;

        // --- V0.3 新增：狙击手焦点监听引擎 ---
        private IntPtr _winEventHook = IntPtr.Zero;
        private Win32API.WinEventDelegate _hookDelegate; // 必须保持为类级变量，防止被垃圾回收(GC)清理
        private uint _currentProcessId;

        #region 生命周期
        public void Initialize()
        {
            try
            {
                _isPluginEnabled = true;
                _disposed = false;

                // 初始化进程 ID 和 钩子委托
                _currentProcessId = Win32API.GetCurrentProcessId();
                _hookDelegate = new Win32API.WinEventDelegate(WinEventCallback);

                lock (_docLock)
                {
                    _hookedDocs.Clear();
                    _pendingWelcomeDocs.Clear();
                }

                _hudManager = new HudManager();
                _chineseCommands = ConfigManager.LoadCommands();

                foreach (Document doc in Application.DocumentManager)
                {
                    AttachEvents(doc);
                }

                Application.DocumentManager.DocumentCreated += OnDocumentCreated;
                Application.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
                Application.DocumentManager.DocumentDestroyed += OnDocumentDestroyed;
                System.Windows.Forms.Application.Idle += OnApplicationIdle;
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
                    StopFocusHook(); // 销毁时务必拔掉系统钩子

                    Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                    Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                    Application.DocumentManager.DocumentDestroyed -= OnDocumentDestroyed;
                    System.Windows.Forms.Application.Idle -= OnApplicationIdle;

                    lock (_docLock)
                    {
                        for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                        {
                            Document doc = _hookedDocs[i];
                            if (doc != null && !doc.IsDisposed) DetachEventsUnsafe(doc);
                        }
                        _hookedDocs.Clear();
                        _pendingWelcomeDocs.Clear();
                    }

                    if (_hudManager != null)
                    {
                        _hudManager.Dispose();
                        _hudManager = null;
                    }
                }
                catch (System.Exception ex) { LogError("Dispose", "资源释放失败", ex); }
            }
            _disposed = true;
        }

        ~PluginMain() { Dispose(false); }
        #endregion

        #region 终极狙击引擎 (系统级事件挂载)

        private void StartFocusHook()
        {
            if (_winEventHook == IntPtr.Zero)
            {
                // 注册系统焦点变化钩子，且仅限当前 CAD 进程内，彻底保证 0 性能损耗与绝对安全
                _winEventHook = Win32API.SetWinEventHook(
                    Win32API.EVENT_OBJECT_FOCUS, Win32API.EVENT_OBJECT_FOCUS,
                    IntPtr.Zero, _hookDelegate, _currentProcessId, 0, Win32API.WINEVENT_OUTOFCONTEXT);
            }
        }

        private void StopFocusHook()
        {
            if (_winEventHook != IntPtr.Zero)
            {
                Win32API.UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 当 CAD 内有任何子窗口（包含刚弹出的多行文字编辑框）拿到焦点时，系统会瞬间回调这个函数
        /// </summary>
        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_isPluginEnabled) return;
            try
            {
                if (eventType == Win32API.EVENT_OBJECT_FOCUS && hwnd != IntPtr.Zero)
                {
                    // 弹夹上膛：直接对准刚刚抢到焦点的新窗口发射中文强制指令！
                    ImeController.ForceChinese(hwnd);
                }
            }
            catch { }
        }

        #endregion

        #region CAD 图纸与环境事件
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
            catch (System.Exception ex) { LogError("OnApplicationIdle", "空闲处理异常", ex); }
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
                    Document doc = _hookedDocs[i];
                    if (doc.IsDisposed || doc.Name == e.FileName)
                    {
                        DetachEventsUnsafe(doc);
                        _hookedDocs.RemoveAt(i);
                    }
                }
                for (int i = _pendingWelcomeDocs.Count - 1; i >= 0; i--)
                {
                    Document doc = _pendingWelcomeDocs[i];
                    if (doc.IsDisposed || doc.Name == e.FileName) _pendingWelcomeDocs.RemoveAt(i);
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
            }
            catch (System.Exception ex) { LogError("DetachEventsUnsafe", "事件解绑失败", ex); }
        }
        #endregion

        #region CAD 命令执行前后拦截
        private void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            if (!_isPluginEnabled) return;
            try
            {
                string globalCmd = e.GlobalCommandName;
                if (!string.IsNullOrEmpty(globalCmd) && globalCmd.StartsWith("'")) return;

                string cmd = globalCmd.Trim().TrimStart('_', '-', '\'', '.');
                if (_chineseCommands.ContainsKey(cmd))
                {
                    // 1. 先对当前的主窗口切一波中文
                    ImeController.ForceChinese(GetCadMainWindowHandle());

                    // 2. 狙击手睁眼：立刻挂上系统钩子，死死盯住接下来弹出的文字输入框
                    StartFocusHook();
                }
            }
            catch (System.Exception ex) { LogError("OnCommandWillStart", "命令拦截异常", ex); }
        }

        private void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (!_isPluginEnabled) return;
            try
            {
                string globalCmd = e.GlobalCommandName;
                if (!string.IsNullOrEmpty(globalCmd) && globalCmd.StartsWith("'")) return;

                // 狙击手闭眼撤退：卸载系统钩子，不浪费一丝性能
                StopFocusHook();

                ImeController.ForceEnglish(GetCadMainWindowHandle());
            }
            catch (System.Exception ex) { LogError("OnCommandEnded", "命令结束异常", ex); }
        }
        #endregion

        #region 插件注册命令
        [CommandMethod("TOGGLEAUTOIME")]
        public void ToggleAutoImeCommand()
        {
            _isPluginEnabled = !_isPluginEnabled;
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                    doc.Editor.WriteMessage(
                        "\n>>> [浅醉·墨语] CAD Auto IME 输入法自动切换助手已 " + (_isPluginEnabled ? "开启" : "关闭") + " <<<\n");
            }
            catch { }
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
                        _chineseCommands = ConfigManager.LoadCommands();
                        if (doc != null && doc.Editor != null)
                            doc.Editor.WriteMessage(
                                "\n>>> [浅醉·墨语] CAD Auto IME 白名单配置已更新，当前共加载 " +
                                ConfigManager.LoadedCustomCount + " 个自定义命令，已立即生效！ <<<\n");
                    }
                }

                if (_isPluginEnabled) ImeController.ForceEnglish(GetCadMainWindowHandle());
            }
            catch (System.Exception ex) { LogError("ShowCustomConfigDialog", "无法打开配置面板", ex); }
        }
        #endregion

        #region 辅助工具方法
        private static IntPtr GetCadMainWindowHandle()
        {
            try { return Application.MainWindow.Handle; }
            catch { return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
        }

        private void LogError(string methodName, string msg, System.Exception ex)
        {
            try
            {
                Document doc = null;
                try { doc = Application.DocumentManager.MdiActiveDocument; } catch { }

                if (doc != null && doc.Editor != null)
                {
                    string errorMsg = msg + (ex != null ? " | 异常: " + ex.Message : "");
                    doc.Editor.WriteMessage("\n>>> [浅醉·墨语]CAD Auto IME [" + methodName + "] 错误: " + errorMsg + " <<<\n");
                }
                System.Diagnostics.Debug.WriteLine("[CADAutoIME-" + methodName + "] " + msg + " " + ex?.ToString());
            }
            catch { }
        }
        #endregion
    }
}