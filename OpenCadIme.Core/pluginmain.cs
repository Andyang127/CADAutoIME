#pragma warning disable CA1416
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using OpenCadIme.Core;

[assembly: ExtensionApplication(typeof(OpenCadIme.PluginMain))]
namespace OpenCadIme
{
    public class PluginMain : IExtensionApplication, IDisposable, System.Windows.Forms.IMessageFilter
    {
        private FocusHookManager _focusManager;
        private CommandInterceptor _commandInterceptor;
        private UI.HudManager _hudManager;

        private bool _isPluginEnabled = true;
        private bool _disposed = false;
        private bool _isFullyInitialized = false;
        private bool _hasHookedStartupEvents = false;

        private HashSet<IntPtr> _welcomedDocs = new HashSet<IntPtr>();
        private string _pendingHudVersion = null;
        private static IntPtr _cachedCadHandle = IntPtr.Zero;
        private bool _isTerminating = false;
        private int _lastDoubleClickTick = 0;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_NCLBUTTONDBLCLK = 0x00A3;

        internal class CadWindowWrapper : System.Windows.Forms.IWin32Window
        {
            private IntPtr _hwnd;
            public CadWindowWrapper(IntPtr handle) { _hwnd = handle; }
            public IntPtr Handle { get { return _hwnd; } }
        }

        public void Initialize()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                Logger.Info("PluginMain", $"{AppConstants.PluginShortName} v{AppConstants.VersionDisplay} 开始挂载...");

                if (Application.DocumentManager != null)
                {
                    Application.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
                    Application.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
                }

                if (TryInitialize(false)) return;

                Application.DocumentManager.DocumentBecameCurrent += OnStartupEvent;
                Application.DocumentManager.DocumentCreated += OnStartupEvent;
                Application.SystemVariableChanged += OnStartupEvent;

                _hasHookedStartupEvents = true;
            }
            catch (System.Exception ex) { Logger.Error("PluginMain", "插件预加载阶段失败", ex); }
        }

        private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            string assemblyName = executingAssembly.GetName().Name;

            if (args.Name.StartsWith(assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                if (args.Name.IndexOf(".resources", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return executingAssembly;
                }
            }
            return null;
        }

        private void OnStartupEvent(object sender, EventArgs e)
        {
            TryInitialize(false);
        }

        private void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null)
            {
                TryPrintDocumentWelcome(e.Document);
            }
        }

        private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            try
            {
                if (e.Document != null && e.Document.UnmanagedObject != IntPtr.Zero)
                {
                    _welcomedDocs.Remove(e.Document.UnmanagedObject);
                }
            }
            catch { }
        }

        private void TryPrintDocumentWelcome(Document doc)
        {
            if (doc == null || doc.Editor == null || !_isPluginEnabled) return;
            try
            {
                IntPtr ptr = doc.UnmanagedObject;
                if (!_welcomedDocs.Contains(ptr))
                {
                    doc.Editor.WriteMessage("\n");
                    doc.Editor.WriteMessage("==============================================================\n");
                    doc.Editor.WriteMessage($"[光雾山绘图定制]{AppConstants.VersionDisplay}已成功启动！\n");
                    if (ConfigManager.LoadedCustomCount > 0)
                        doc.Editor.WriteMessage(">>> 已成功从 AutoImeCommands.txt 载入" + ConfigManager.LoadedCustomCount + "个自定义白名单命令 <<<\n");
                    doc.Editor.WriteMessage("------------------------------------------------------------\n");
                    doc.Editor.WriteMessage("💡 输入命令 TOGGLEAUTOIME 可【开启/关闭】本程序\n");
                    doc.Editor.WriteMessage("💡 输入命令 CUSTOMAUTOIME 可调用自定义配置面板\n");
                    doc.Editor.WriteMessage("==============================================================\n");
                    doc.Editor.WriteMessage("\n");
                    _welcomedDocs.Add(ptr);
                }
            }
            catch { }
        }

        private bool CheckAndSetHudShownFlag()
        {
            try
            {
                string cadVersion = Application.Version.ToString();
                string regPath = $@"{AppConstants.RegistryPath}\WelcomeHud";

                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath))
                {
                    if (key.GetValue(cadVersion) != null)
                    {
                        return false;
                    }
                    else
                    {
                        key.SetValue(cadVersion, AppConstants.VersionFull);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryInitialize(bool isManualCommand = false)
        {
            if (_disposed || _isFullyInitialized) return true;

            Document doc = null;
            try
            {
                if (Application.DocumentManager.Count > 0)
                    doc = Application.DocumentManager.MdiActiveDocument;
            }
            catch { return false; }

            if (doc == null || doc.Editor == null) return false;

            try
            {
                ImeController.Initialize();

                var whitelist = OpenCadIme.Core.ConfigManager.LoadCommands();
                _commandInterceptor = new CommandInterceptor(whitelist);
                _commandInterceptor.CommandStateChanged += OnCommandStateChanged;

                _focusManager = new FocusHookManager();
                _focusManager.FocusChanged += OnFocusChanged;
                _focusManager.StartListening();

                try { System.Windows.Forms.Application.AddMessageFilter(this); } catch { }

                _hudManager = new UI.HudManager();

                _isFullyInitialized = true;
                EnforceImeState();

                if (!isManualCommand && CheckAndSetHudShownFlag())
                {
                    _pendingHudVersion = AppConstants.VersionFull;
                    System.Windows.Forms.Application.Idle += OnCadIdleToShowHud;
                }

                TryPrintDocumentWelcome(doc);

                if (_hasHookedStartupEvents)
                {
                    Application.DocumentManager.DocumentBecameCurrent -= OnStartupEvent;
                    Application.DocumentManager.DocumentCreated -= OnStartupEvent;
                    Application.SystemVariableChanged -= OnStartupEvent;
                    _hasHookedStartupEvents = false;
                }

                Logger.Info("PluginMain", "基于行为时序的高级状态机已启动 (向下兼容旧版)！");
                return true;
            }
            catch (System.Exception ex)
            {
                Logger.Error("PluginMain", "核心初始化异常，等待下次重试", ex);
                _isFullyInitialized = false;
                return false;
            }
        }

        public bool PreFilterMessage(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_LBUTTONDBLCLK || m.Msg == WM_NCLBUTTONDBLCLK)
            {
                if (_isPluginEnabled && _isFullyInitialized)
                {
                    _lastDoubleClickTick = Environment.TickCount;
                }
            }
            return false;
        }

        private void OnCadIdleToShowHud(object sender, EventArgs e)
        {
            System.Windows.Forms.Application.Idle -= OnCadIdleToShowHud;

            if (_hudManager != null && !string.IsNullOrEmpty(_pendingHudVersion))
            {
                try
                {
                    if (Application.DocumentManager.Count > 0)
                    {
                        Document doc = Application.DocumentManager.MdiActiveDocument;
                        if (doc != null) _hudManager.ShowWelcomeMessage(doc, _pendingHudVersion);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Error("PluginMain", "HUD 延迟挂载失败", ex);
                }
            }
        }

        private void OnCommandStateChanged(object sender, EventArgs e)
        {
            if (!_isPluginEnabled || !_isFullyInitialized) return;
            EnforceImeState();
        }

        private void OnFocusChanged(object sender, EventArgs e)
        {
            EnforceImeState();
        }

        private bool _isEnforcingIme = false;
        private static readonly string[] _englishOnlyClasses = { "acautocomp", "#32768", "accmdlineui" };
        private System.Text.StringBuilder _classNameBuffer = new System.Text.StringBuilder(256);


        private void EnforceImeState()
        {
            if (_isEnforcingIme || !_isPluginEnabled || !_isFullyInitialized) return;

            try
            {
                _isEnforcingIme = true;

                IntPtr currentFocus = _focusManager.CurrentFocusHwnd;
                if (currentFocus == IntPtr.Zero)
                    currentFocus = ImeController.GetRealFocusWindow(GetCadMainWindowHandle());
                _classNameBuffer.Length = 0;
                OpenCadIme.Interop.Win32API.GetClassName(currentFocus, _classNameBuffer, _classNameBuffer.Capacity);
                string clsName = _classNameBuffer.ToString();

                bool forceEnglishClass = false;
                foreach (var cls in _englishOnlyClasses)
                {
                    if (clsName.IndexOf(cls, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        forceEnglishClass = true;
                        break;
                    }
                }

                if (forceEnglishClass)
                {
                    ImeController.ForceEnglish(currentFocus);
                    return;
                }

                CommandCategory activeCategory = _commandInterceptor.GetActiveCommandCategory();
                bool targetIsChinese = false;

                if (activeCategory == CommandCategory.Inline)
                {
                    targetIsChinese = true;
                }
                else if (activeCategory == CommandCategory.Windowed)
                {
                    bool isCanvas = clsName.IndexOf("afxframeorview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    clsName.IndexOf("acuiview", StringComparison.OrdinalIgnoreCase) >= 0;
                    targetIsChinese = !isCanvas;
                }
                else
                {
                    bool isStrictTextBox = clsName.Equals("edit", StringComparison.OrdinalIgnoreCase) ||
                                           clsName.IndexOf("richedit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           clsName.IndexOf("windowsforms10.edit", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isStrictTextBox)
                    {
                        int elapsedMs = unchecked(Environment.TickCount - _lastDoubleClickTick);

                        if (_lastDoubleClickTick != 0 && elapsedMs > 0 && elapsedMs < 1500)
                        {
                            targetIsChinese = true;
                            _lastDoubleClickTick = 0;
                        }
                        else
                        {
                            targetIsChinese = false;
                        }
                    }
                    else
                    {
                        targetIsChinese = false;
                    }
                }

                if (targetIsChinese)
                {
                    ImeController.ForceChinese(currentFocus);
                }
                else
                {
                    ImeController.ForceEnglish(currentFocus);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error("PluginMain", "强制输入法状态时发生异常", ex);
            }
            finally
            {
                _isEnforcingIme = false;
            }
        }

        [CommandMethod("TOGGLEAUTOIME")]
        public void ToggleAutoImeCommand()
        {
            if (!TryInitialize(true))
            {
                System.Windows.Forms.MessageBox.Show($"[{AppConstants.PluginShortName}] 核心尚未就绪，或图纸未激活，请稍后再试！", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            _isPluginEnabled = !_isPluginEnabled;
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                {
                    doc.Editor.WriteMessage($"\n>>> [光雾山绘图定制] 已 {(_isPluginEnabled ? "开启" : "关闭")} <<<\n");
                }

                if (!_isPluginEnabled)
                {
                    _focusManager?.StopListening();
                    ImeController.ForceEnglish(ImeController.GetRealFocusWindow(GetCadMainWindowHandle()));
                }
                else
                {
                    _focusManager?.StartListening();
                    OnCommandStateChanged(this, EventArgs.Empty);
                }
            }
            catch (System.Exception ex) { Logger.Error("ToggleAutoImeCommand", "状态切换异常", ex); }
        }

        [CommandMethod("CUSTOMAUTOIME")]
        public void ShowCustomConfigDialog()
        {
            if (!TryInitialize(true))
            {
                System.Windows.Forms.MessageBox.Show($"[{AppConstants.PluginShortName}] 核心尚未就绪，或图纸未激活，请稍后再试！", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;

                if (_hudManager != null)
                {
                    _hudManager.Dispose();
                    _hudManager = null;
                }

#if USE_WPF
                ShowWpfConfigDialogIsolated(doc);
#else
                ShowWinFormsConfigDialogIsolated(doc);
#endif
                if (_isPluginEnabled)
                {
                    OnCommandStateChanged(this, EventArgs.Empty);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"命令执行异常！\n\n原因: {ex.Message}", $"致命错误 - {AppConstants.PluginShortName}", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

#if USE_WPF
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShowWpfConfigDialogIsolated(Document doc)
        {
            try
            {
                OpenCadIme.UI.ModernWpf.ConfigWindow wpfConfigWindow = new OpenCadIme.UI.ModernWpf.ConfigWindow();
                bool? result = OpenCadIme.UI.ModernWpf.WindowManager.ShowModal(wpfConfigWindow);

                if (result == true)
                {
                    _commandInterceptor?.UpdateWhitelist(OpenCadIme.Core.ConfigManager.LoadCommands());
                    doc?.Editor?.WriteMessage($"\n>>> [光雾山绘图定制] 配置已更新！ <<<\n");
                }
            }
            catch (System.Exception wpfEx)
            {
                System.Windows.Forms.MessageBox.Show($"WPF 配置面板构建崩溃！\n\n原因: {wpfEx.Message}\n\n内部错误: {wpfEx.InnerException?.Message}", $"致命错误 - {AppConstants.PluginShortName}", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
#else
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShowWinFormsConfigDialogIsolated(Document doc)
        {
            try
            {
                using (OpenCadIme.UI.LegacyForm.ConfigForm form = new OpenCadIme.UI.LegacyForm.ConfigForm())
                {
                    System.Windows.Forms.DialogResult dr = System.Windows.Forms.DialogResult.Cancel;
                    IntPtr hwnd = GetCadMainWindowHandle();
                    System.Reflection.MethodInfo showModalMethod = typeof(Application).GetMethod("ShowModalDialog", new Type[] { typeof(System.Windows.Forms.Form) });

                    if (showModalMethod != null)
                    {
                        dr = (System.Windows.Forms.DialogResult)showModalMethod.Invoke(null, new object[] { form });
                    }
                    else if (hwnd != IntPtr.Zero)
                    {
                        CadWindowWrapper owner = new CadWindowWrapper(hwnd);
                        dr = form.ShowDialog(owner);
                    }
                    else
                    {
                        dr = form.ShowDialog();
                    }

                    if (dr == System.Windows.Forms.DialogResult.OK)
                    {
                        _commandInterceptor?.UpdateWhitelist(OpenCadIme.Core.ConfigManager.LoadCommands());
                        doc?.Editor?.WriteMessage($"\n>>> [光雾山绘图定制] 配置已更新！ <<<\n");
                    }
                }
            }
            catch (System.Exception wfEx)
            {
                System.Windows.Forms.MessageBox.Show($"传统配置面板加载崩溃！\n\n原因: {wfEx.Message}", $"致命错误 - {AppConstants.PluginShortName}", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
#endif

        private static IntPtr GetCadMainWindowHandle()
        {
            if (_cachedCadHandle != IntPtr.Zero) return _cachedCadHandle;
            try
            {
                if (Application.MainWindow != null && Application.MainWindow.Handle != IntPtr.Zero)
                {
                    _cachedCadHandle = Application.MainWindow.Handle;
                    return _cachedCadHandle;
                }
            }
            catch { }

            try
            {
                _cachedCadHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                return _cachedCadHandle;
            }
            catch { return IntPtr.Zero; }
        }

        public void Terminate()
        {
            _isTerminating = true;
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                System.Windows.Forms.Application.Idle -= OnCadIdleToShowHud;
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

                if (!_isTerminating)
                {
                    try
                    {
                        if (Application.DocumentManager != null)
                        {
                            Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                            Application.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
                        }

                        if (_hasHookedStartupEvents)
                        {
                            Application.DocumentManager.DocumentBecameCurrent -= OnStartupEvent;
                            Application.DocumentManager.DocumentCreated -= OnStartupEvent;
                            Application.SystemVariableChanged -= OnStartupEvent;
                            _hasHookedStartupEvents = false;
                        }
                    }
                    catch { }
                }

                if (_focusManager != null)
                {
                    _focusManager.FocusChanged -= OnFocusChanged;
                    _focusManager.Dispose();
                }

                try { System.Windows.Forms.Application.RemoveMessageFilter(this); } catch { }

                _commandInterceptor?.Dispose();
                _hudManager?.Dispose();

                try
                {
                    ImeController.DisposeTsfEngine();
                }
                catch { }

                _cachedCadHandle = IntPtr.Zero;

                if (!_isTerminating)
                {
                    Logger.Info("PluginMain", "插件资源清理完毕，已安全退出。");
                }
            }
            catch
            {
            }
            finally { _disposed = true; }
        }
    }
}