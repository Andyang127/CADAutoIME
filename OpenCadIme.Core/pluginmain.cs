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
    public class PluginMain : IExtensionApplication, IDisposable
    {
        private FocusHookManager _focusManager;
        private CommandInterceptor _commandInterceptor;
        private UI.HudManager _hudManager;

        private bool _isPluginEnabled = true;
        private bool _disposed = false;
        private bool _isFullyInitialized = false;
        private bool _hasHookedStartupEvents = false;

        private IntPtr _lastProcessedHwnd = IntPtr.Zero;
        private string _lastProcessedClassName = string.Empty;

        private bool _isCurrentlyInEditor = false;
        private bool _hasForcedChineseForThisEditor = false;
        private bool _hasForcedEnglishForThisCanvas = false;

        private HashSet<IntPtr> _welcomedDocs = new HashSet<IntPtr>();
        private string _pendingHudVersion = null;

        // 【核心优化】：主窗口句柄缓存，避免高频调用 Process.GetCurrentProcess() 导致 CPU 飙升
        private static IntPtr _cachedCadHandle = IntPtr.Zero;

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
                    doc.Editor.WriteMessage($"[[浅醉·墨语]CAD Auto IME 输入法自动切换程序]{AppConstants.VersionDisplay}已成功启动！\n");
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
                // 【洁癖级修复】：统一采用 AppConstants 中的标准注册表路径，终结混乱
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
                return false; // 如果因权限问题读写失败，默认不再显示，以免惹怒用户
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

                _hudManager = new UI.HudManager();
                ImeController.ForceEnglish(GetCadMainWindowHandle());

                if (!isManualCommand && CheckAndSetHudShownFlag())
                {
                    _pendingHudVersion = AppConstants.VersionFull;
                    System.Windows.Forms.Application.Idle += OnCadIdleToShowHud;
                }

                TryPrintDocumentWelcome(doc);

                _isFullyInitialized = true;
                if (_hasHookedStartupEvents)
                {
                    Application.DocumentManager.DocumentBecameCurrent -= OnStartupEvent;
                    Application.DocumentManager.DocumentCreated -= OnStartupEvent;
                    Application.SystemVariableChanged -= OnStartupEvent;
                    _hasHookedStartupEvents = false;
                }

                Logger.Info("PluginMain", "跨版本安全初始化圆满完成！");
                return true;
            }
            catch (System.Exception ex)
            {
                Logger.Error("PluginMain", "核心初始化异常，等待下次重试", ex);
                _isFullyInitialized = false;
                return false;
            }
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

            CommandCategory activeCategory = _commandInterceptor.GetActiveCommandCategory();

            if (activeCategory != CommandCategory.None)
            {
                _focusManager.StartListening();

                _isCurrentlyInEditor = false;
                _hasForcedChineseForThisEditor = false;
                _hasForcedEnglishForThisCanvas = false;
                _lastProcessedHwnd = IntPtr.Zero;
                _lastProcessedClassName = string.Empty;

                EnforceImeState();
            }
            else
            {
                _focusManager.StopListening();
                ImeController.ForceEnglish(GetCadMainWindowHandle());
                _lastProcessedHwnd = IntPtr.Zero;
            }
        }

        private void OnFocusChanged(object sender, EventArgs e)
        {
            EnforceImeState();
        }

        private void EnforceImeState()
        {
            // 【核心优化】：增加全覆盖的异常捕获。此函数每秒可能被调用多次，决不能抛出异常阻断 CAD 渲染
            try
            {
                if (!_isPluginEnabled || !_isFullyInitialized) return;

                CommandCategory activeCategory = _commandInterceptor.GetActiveCommandCategory();
                if (activeCategory == CommandCategory.None) return;

                IntPtr currentFocus = _focusManager.CurrentFocusHwnd;
                if (currentFocus == IntPtr.Zero) currentFocus = GetCadMainWindowHandle();

                System.Text.StringBuilder classNameBuffer = new System.Text.StringBuilder(256);
                OpenCadIme.Interop.Win32API.GetClassName(currentFocus, classNameBuffer, classNameBuffer.Capacity);
                string className = classNameBuffer.ToString();

                if (currentFocus == _lastProcessedHwnd && string.Equals(className, _lastProcessedClassName, StringComparison.OrdinalIgnoreCase)) return;

                if (className.IndexOf("AcAutoComp", StringComparison.OrdinalIgnoreCase) >= 0 || className == "#32768") return;

                bool isEditor = IsEditorClass(className);

                if (activeCategory == CommandCategory.Windowed)
                {
                    if (isEditor)
                    {
                        _isCurrentlyInEditor = true;
                        _hasForcedEnglishForThisCanvas = false;

                        if (!_hasForcedChineseForThisEditor)
                        {
                            ImeController.ForceChinese(currentFocus);
                            _hasForcedChineseForThisEditor = true;
                        }
                    }
                    else
                    {
                        if (_isCurrentlyInEditor)
                        {
                            if (!_hasForcedEnglishForThisCanvas)
                            {
                                ImeController.ForceEnglish(currentFocus);
                                _hasForcedEnglishForThisCanvas = true;
                            }
                            _isCurrentlyInEditor = false;
                            _hasForcedChineseForThisEditor = false;
                        }
                    }
                }
                else if (activeCategory == CommandCategory.Inline)
                {
                    if (!_hasForcedChineseForThisEditor)
                    {
                        ImeController.ForceChinese(currentFocus);
                        _hasForcedChineseForThisEditor = true;
                    }
                }

                _lastProcessedHwnd = currentFocus;
                _lastProcessedClassName = className;
            }
            catch (System.Exception ex)
            {
                Logger.Error("PluginMain", "强制输入法状态时发生异常", ex);
            }
        }

        private bool IsEditorClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            string clsLower = className.ToLowerInvariant();

            if (clsLower.Contains("afxframeorview") || clsLower.Contains("acuiview") || clsLower.Contains("afxmdiframe"))
                return false;

            if (clsLower.Contains("accmdlineui"))
                return false;

            return true;
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
                    doc.Editor.WriteMessage($"\n>>> [{AppConstants.PluginShortName}] 已 {(_isPluginEnabled ? "开启" : "关闭")} <<<\n");
                }

                if (!_isPluginEnabled)
                {
                    _focusManager?.StopListening();
                    ImeController.ForceEnglish(GetCadMainWindowHandle());
                }
                else
                {
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
                    doc?.Editor?.WriteMessage($"\n>>> [{AppConstants.PluginShortName}] 配置已更新！ <<<\n");
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
                        doc?.Editor?.WriteMessage($"\n>>> [{AppConstants.PluginShortName}] 配置已更新！ <<<\n");
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

        public void Terminate() { Dispose(); }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                // 【终极修复】：在此处增加安全兜底，防止插件被强杀时闲置事件未注销导致的泄漏
                System.Windows.Forms.Application.Idle -= OnCadIdleToShowHud;

                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

                if (Application.DocumentManager != null)
                {
                    Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                }

                if (_hasHookedStartupEvents)
                {
                    Application.DocumentManager.DocumentBecameCurrent -= OnStartupEvent;
                    Application.DocumentManager.DocumentCreated -= OnStartupEvent;
                    Application.SystemVariableChanged -= OnStartupEvent;
                    _hasHookedStartupEvents = false;
                }

                if (_focusManager != null)
                {
                    _focusManager.FocusChanged -= OnFocusChanged;
                    _focusManager.Dispose();
                }

                _commandInterceptor?.Dispose();
                _hudManager?.Dispose();

                ImeController.DisposeTsfEngine();
                _cachedCadHandle = IntPtr.Zero;

                Logger.Info("PluginMain", "插件资源清理完毕，已安全退出。");
            }
            catch (System.Exception ex) { Logger.Error("Dispose", "销毁资源时发生异常", ex); }
            finally { _disposed = true; }
        }
    }
}