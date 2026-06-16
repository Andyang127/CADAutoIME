using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

[assembly: ExtensionApplication(typeof(OpenCadIme.Switcher))]

namespace OpenCadIme
{
    /// <summary>
    /// CAD 输入法自动切换主程序 - 兼容 CAD 2007-2027 全版本
    /// </summary>
    public class Switcher : IExtensionApplication, IDisposable
    {
        #region Win32 API 声明
        [DllImport("imm32.dll")] public static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] public static extern bool ImmSetOpenStatus(IntPtr hIMC, bool b);
        [DllImport("imm32.dll")] public static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr hIMC);

        [DllImport("user32.dll")] static extern IntPtr GetFocus();
        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        #endregion

        #region 常量定义
        private const string AppVersion = "0.2";
        private const int COUNTDOWN_SECONDS = 15;
        private const double VISUAL_COMPENSATION_MS = 300;
        private const int HUD_FADE_INTERVAL_MS = 15;
        private const double HUD_FADE_INCREMENT = 0.12;
        private const int HUD_TIMER_INTERVAL_MS = 30;
        private const string REGISTRY_PATH = @"Software\QianZuiMoYu\CADAutoIme";
        #endregion

        #region 私有字段
        private Dictionary<string, bool> _chineseCommands;
        private List<Document> _hookedDocs = new List<Document>();
        private List<Document> _pendingWelcomeDocs = new List<Document>();
        private bool _isPluginEnabled = true;
        private System.Windows.Forms.Form _hudForm;
        private System.Windows.Forms.Label _hudCountdownLabel;
        private System.Windows.Forms.Timer _hudTimer;

        private List<System.Windows.Forms.Timer> _activeTimers = new List<System.Windows.Forms.Timer>();
        private readonly object _timerLock = new object();
        private bool _disposed;

        private Font _fontTitle;
        private Font _fontVer;
        private Font _fontSlogan;
        private Font _fontOnceTip;
        private Font _fontCountdown;
        #endregion

        #region 日志处理
        private void LogError(string methodName, string msg, System.Exception ex)
        {
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null)
                {
                    string errorMsg = msg;
                    if (ex != null) errorMsg = msg + " | 异常: " + ex.Message;
                    doc.Editor.WriteMessage(
                        "\n>>> [浅醉·墨语]CAD Auto IME [" + methodName + "] 错误: " + errorMsg + " <<<\n");
                }
                string debugMsg = "[CADAutoIME-" + methodName + "] " + msg;
                if (ex != null) debugMsg = debugMsg + " " + ex.ToString();
                System.Diagnostics.Debug.WriteLine(debugMsg);
            }
            catch (System.Exception) { }
        }
        #endregion

        #region 生命周期
        public void Initialize()
        {
            try
            {
                _isPluginEnabled = true;
                _disposed = false;
                _hookedDocs.Clear();
                _pendingWelcomeDocs.Clear();
                CleanupAllTimers();

                _fontTitle = CreateSafeFont("微软雅黑", 16, FontStyle.Bold);
                _fontVer = CreateSafeFont("Consolas", 12, FontStyle.Bold | FontStyle.Italic);
                _fontSlogan = CreateSafeFont("微软雅黑", 9.5f, FontStyle.Regular);
                _fontOnceTip = CreateSafeFont("微软雅黑", 9, FontStyle.Regular);
                _fontCountdown = CreateSafeFont("微软雅黑", 9, FontStyle.Regular);

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
                    DisposeHud();
                    CleanupAllTimers();

                    Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                    Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                    Application.DocumentManager.DocumentDestroyed -= OnDocumentDestroyed;
                    System.Windows.Forms.Application.Idle -= OnApplicationIdle;

                    for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                    {
                        Document doc = _hookedDocs[i];
                        if (doc != null && !doc.IsDisposed) DetachEvents(doc);
                    }
                    _hookedDocs.Clear();
                    _pendingWelcomeDocs.Clear();

                    if (_fontTitle != null) { _fontTitle.Dispose(); _fontTitle = null; }
                    if (_fontVer != null) { _fontVer.Dispose(); _fontVer = null; }
                    if (_fontSlogan != null) { _fontSlogan.Dispose(); _fontSlogan = null; }
                    if (_fontOnceTip != null) { _fontOnceTip.Dispose(); _fontOnceTip = null; }
                    if (_fontCountdown != null) { _fontCountdown.Dispose(); _fontCountdown = null; }
                }
                catch (System.Exception ex) { LogError("Dispose", "资源释放失败", ex); }
            }
            _disposed = true;
        }

        ~Switcher() { Dispose(false); }

        private void CleanupAllTimers()
        {
            lock (_timerLock)
            {
                foreach (System.Windows.Forms.Timer timer in _activeTimers)
                {
                    if (timer != null) { try { timer.Stop(); timer.Dispose(); } catch { } }
                }
                _activeTimers.Clear();
            }
        }

        private void TrackTimer(System.Windows.Forms.Timer timer) { lock (_timerLock) _activeTimers.Add(timer); }
        private void UntrackTimer(System.Windows.Forms.Timer timer) { lock (_timerLock) _activeTimers.Remove(timer); }
        #endregion

        #region 文档事件
        private void OnApplicationIdle(object sender, EventArgs e)
        {
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null && _pendingWelcomeDocs.Contains(doc))
                {
                    _pendingWelcomeDocs.Remove(doc);
                    ShowWelcomeMessage(doc);
                }
            }
            catch (System.Exception ex) { LogError("OnApplicationIdle", "空闲处理异常", ex); }
        }

        private void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null)
            {
                AttachEvents(e.Document);
                // ✅ 修复：在图纸切换的瞬间，强制恢复英文状态，确保干净的环境
                if (_isPluginEnabled) SwitchIme(false);
            }
        }

        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null) AttachEvents(e.Document);
        }

        private void OnDocumentDestroyed(object sender, DocumentDestroyedEventArgs e)
        {
            for (int i = _hookedDocs.Count - 1; i >= 0; i--)
            {
                Document doc = _hookedDocs[i];
                if (doc.IsDisposed || doc.Name == e.FileName)
                {
                    DetachEvents(doc);
                    _hookedDocs.RemoveAt(i);
                }
            }
            for (int i = _pendingWelcomeDocs.Count - 1; i >= 0; i--)
            {
                Document doc = _pendingWelcomeDocs[i];
                if (doc.IsDisposed || doc.Name == e.FileName) _pendingWelcomeDocs.RemoveAt(i);
            }
        }

        private void AttachEvents(Document doc)
        {
            if (doc == null || doc.IsDisposed) return;
            if (!_hookedDocs.Contains(doc))
            {
                doc.CommandWillStart += OnCommandWillStart;
                doc.CommandEnded += OnCommandEnded;
                doc.CommandCancelled += OnCommandEnded; // Cancel 视同 Ended
                _hookedDocs.Add(doc);
                if (!_pendingWelcomeDocs.Contains(doc)) _pendingWelcomeDocs.Add(doc);
            }
        }

        private void DetachEvents(Document doc)
        {
            try
            {
                if (doc == null || doc.IsDisposed) return;
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandEnded;
            }
            catch (System.Exception ex) { LogError("DetachEvents", "事件解绑失败", ex); }
        }
        #endregion

        #region 插件命令
        [CommandMethod("TOGGLEAUTOIME")]
        public void ToggleAutoImeCommand()
        {
            _isPluginEnabled = !_isPluginEnabled;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null && doc.Editor != null)
                doc.Editor.WriteMessage(
                    "\n>>> [浅醉·墨语] CAD Auto IME 输入法自动切换助手已 " + (_isPluginEnabled ? "开启" : "关闭") + " <<<\n");
        }

        [CommandMethod("CUSTOMAUTOIME")]
        public void ShowCustomConfigDialog()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
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
                // ✅ 修复：配置面板关闭后（此时焦点刚从WinForms交还给CAD），强制切回英文，解决 customautoime 卡中文的问题
                if (_isPluginEnabled) SwitchIme(false);
            }
            catch (System.Exception ex) { LogError("ShowCustomConfigDialog", "无法打开配置面板", ex); }
        }

        private static IntPtr GetCadMainWindowHandle()
        {
            try { return Application.MainWindow.Handle; }
            catch { return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
        }
        #endregion

        #region HUD 与命令行欢迎
        private void DisposeHud()
        {
            try
            {
                if (_hudTimer != null)
                {
                    _hudTimer.Stop();
                    UntrackTimer(_hudTimer);
                    _hudTimer.Dispose();
                    _hudTimer = null;
                }
                if (_hudCountdownLabel != null && !_hudCountdownLabel.IsDisposed)
                {
                    _hudCountdownLabel.Dispose();
                    _hudCountdownLabel = null;
                }
                if (_hudForm != null && !_hudForm.IsDisposed)
                {
                    System.Drawing.Region oldRegion = _hudForm.Region;
                    _hudForm.Close();
                    _hudForm.Dispose();
                    _hudForm = null;
                    if (oldRegion != null) oldRegion.Dispose();
                }
            }
            catch (System.Exception ex) { LogError("DisposeHud", "HUD释放异常", ex); }
        }

        private Point CalculateHudTargetPosition(IntPtr cadHandle, int realWidth, int realHeight)
        {
            const int fallbackX = -5000;
            const int fallbackY = -5000;
            if (cadHandle == IntPtr.Zero) return new Point(fallbackX, fallbackY);

            RECT cadRect;
            if (!GetWindowRect(cadHandle, out cadRect)) return new Point(fallbackX, fallbackY);

            int cadWidth = cadRect.Right - cadRect.Left;
            int cadHeight = cadRect.Bottom - cadRect.Top;
            if (cadWidth <= 0 || cadHeight <= 0) return new Point(fallbackX, fallbackY);

            int targetX = cadRect.Left + (int)(cadWidth * 2.0 / 3.0);
            int targetY = cadRect.Top + (int)(cadHeight * 2.0 / 3.0);

            if (targetX + realWidth > cadRect.Right) targetX = cadRect.Right - realWidth - 10;
            if (targetY + realHeight > cadRect.Bottom) targetY = cadRect.Bottom - realHeight - 10;

            return new Point(targetX, targetY);
        }

        private void ShowWelcomeMessage(Document doc)
        {
            if (doc == null || doc.Editor == null) return;

            doc.Editor.WriteMessage("\n====================================================\n");
            doc.Editor.WriteMessage("[[浅醉·墨语]CAD Auto IME 输入法自动切换程序] V" + AppVersion + " 已成功启动！\n");
            if (ConfigManager.LoadedCustomCount > 0)
                doc.Editor.WriteMessage(">>> 已成功从 AutoImeCommands.txt 载入 " + ConfigManager.LoadedCustomCount + " 个自定义白名单命令 <<<\n");
            doc.Editor.WriteMessage("----------------------------------------------------\n");
            doc.Editor.WriteMessage("💡 输入命令 TOGGLEAUTOIME 可开启/关闭本插件\n");
            doc.Editor.WriteMessage("💡 输入命令 CUSTOMAUTOIME 可调出白名单配置面板\n");
            doc.Editor.WriteMessage("====================================================\n");

            string cadInternalVersion = Application.Version.Major.ToString() + "_" + Application.Version.Minor.ToString();
            string regMarker = "HasShownWelcome_V" + AppVersion + "_CAD" + cadInternalVersion;

            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key.GetValue(regMarker) != null) return;
                    key.SetValue(regMarker, 1);
                }
            }
            catch { /* 静默失败 */ }

            ShowHudWelcomeWindow();
        }

        private void ShowHudWelcomeWindow()
        {
            try
            {
                DisposeHud();

                _hudForm = new System.Windows.Forms.Form();
                _hudForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                _hudForm.BackColor = Color.FromArgb(45, 52, 54);
                _hudForm.TopMost = true;
                _hudForm.ShowInTaskbar = false;
                _hudForm.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                _hudForm.Opacity = 0;
                _hudForm.AutoSize = false;

                System.Windows.Forms.TableLayoutPanel table = new System.Windows.Forms.TableLayoutPanel();
                table.AutoSize = true;
                table.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
                table.BackColor = Color.Transparent;
                table.Padding = new System.Windows.Forms.Padding(20, 15, 20, 15);
                table.ColumnCount = 2;
                table.RowCount = 3;
                table.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
                table.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
                table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
                table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
                table.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));

                System.Windows.Forms.FlowLayoutPanel titlePanel = new System.Windows.Forms.FlowLayoutPanel();
                titlePanel.AutoSize = true;
                titlePanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
                titlePanel.WrapContents = false;
                titlePanel.Margin = new System.Windows.Forms.Padding(0);

                System.Windows.Forms.Label lblTitle = new System.Windows.Forms.Label();
                lblTitle.Text = "CAD Auto IME 输入法自动切换助手";
                lblTitle.AutoSize = true;
                lblTitle.Font = _fontTitle;
                lblTitle.ForeColor = Color.White;
                lblTitle.Anchor = System.Windows.Forms.AnchorStyles.Bottom;

                System.Windows.Forms.Label lblVer = new System.Windows.Forms.Label();
                lblVer.Text = "v" + AppVersion;
                lblVer.AutoSize = true;
                lblVer.Font = _fontVer;
                lblVer.ForeColor = Color.FromArgb(0, 210, 211);
                lblVer.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
                lblVer.Margin = new System.Windows.Forms.Padding(6, 0, 0, 3);

                titlePanel.Controls.Add(lblTitle);
                titlePanel.Controls.Add(lblVer);
                table.Controls.Add(titlePanel, 0, 0);
                table.SetColumnSpan(titlePanel, 2);

                System.Windows.Forms.Label lblSlogan = new System.Windows.Forms.Label();
                lblSlogan.Text = "让每一位设计师都专注于创作，而非输入法切换";
                lblSlogan.AutoSize = true;
                lblSlogan.Font = _fontSlogan;
                lblSlogan.ForeColor = Color.DarkGray;
                lblSlogan.Margin = new System.Windows.Forms.Padding(2, 5, 0, 15);
                table.Controls.Add(lblSlogan, 0, 1);
                table.SetColumnSpan(lblSlogan, 2);

                System.Windows.Forms.Label lblOnceTip = new System.Windows.Forms.Label();
                lblOnceTip.Text = "* 注：本提示只会在第一次启动对应CAD版本时显示一次";
                lblOnceTip.AutoSize = true;
                lblOnceTip.Font = _fontOnceTip;
                lblOnceTip.ForeColor = Color.FromArgb(255, 82, 82);
                lblOnceTip.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;

                _hudCountdownLabel = new System.Windows.Forms.Label();
                _hudCountdownLabel.Text = "(" + COUNTDOWN_SECONDS + "s 后自动关闭)";
                _hudCountdownLabel.AutoSize = true;
                _hudCountdownLabel.Font = _fontCountdown;
                _hudCountdownLabel.ForeColor = Color.FromArgb(255, 165, 0);
                _hudCountdownLabel.Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;

                table.Controls.Add(lblOnceTip, 0, 2);
                table.Controls.Add(_hudCountdownLabel, 1, 2);

                _hudForm.Controls.Add(table);
                table.PerformLayout();
                _hudForm.Size = table.PreferredSize;
                _hudForm.Paint += new System.Windows.Forms.PaintEventHandler(HudForm_Paint);

                int style = GetWindowLong(_hudForm.Handle, GWL_EXSTYLE);
                SetWindowLong(_hudForm.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);

                SetupHudTimers();
                _hudForm.Show();
            }
            catch (System.Exception ex) { LogError("ShowHudWelcomeWindow", "HUD悬浮窗加载失败", ex); }
        }

        private void HudForm_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            int realW = _hudForm.Width;
            int realH = _hudForm.Height;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                int radius = 16;
                path.AddArc(0, 0, radius, radius, 180, 90);
                path.AddArc(realW - 1 - radius, 0, radius, radius, 270, 90);
                path.AddArc(realW - 1 - radius, realH - 1 - radius, radius, radius, 0, 90);
                path.AddArc(0, realH - 1 - radius, radius, radius, 90, 90);
                path.CloseAllFigures();

                using (Pen borderPen = new Pen(Color.FromArgb(90, 90, 90), 1.5f))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }
            }
        }

        private void SetupHudTimers()
        {
            bool countdownStarted = false;
            DateTime deadline = DateTime.MaxValue;
            int lastDisplayedSecond = COUNTDOWN_SECONDS;

            _hudForm.Shown += delegate (object sender, EventArgs e)
            {
                _hudForm.BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    IntPtr hrgn = CreateRoundRectRgn(0, 0, _hudForm.Width, _hudForm.Height, 16, 16);
                    System.Drawing.Region oldRegion = _hudForm.Region;
                    _hudForm.Region = System.Drawing.Region.FromHrgn(hrgn);
                    if (oldRegion != null) oldRegion.Dispose();
                    DeleteObject(hrgn);

                    IntPtr cadHandle = GetCadMainWindowHandle();
                    Point targetPos = CalculateHudTargetPosition(cadHandle, _hudForm.Width, _hudForm.Height);
                    _hudForm.Location = targetPos;

                    System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();
                    fadeTimer.Interval = HUD_FADE_INTERVAL_MS;
                    TrackTimer(fadeTimer);

                    fadeTimer.Tick += delegate (object s, EventArgs a)
                    {
                        try
                        {
                            if (_hudForm == null || _hudForm.IsDisposed)
                            {
                                fadeTimer.Stop(); UntrackTimer(fadeTimer); fadeTimer.Dispose(); return;
                            }
                            if (_hudForm.Opacity < 0.95)
                            {
                                _hudForm.Opacity = _hudForm.Opacity + HUD_FADE_INCREMENT;
                            }
                            else
                            {
                                fadeTimer.Stop(); UntrackTimer(fadeTimer); fadeTimer.Dispose();
                                deadline = DateTime.Now.AddSeconds(COUNTDOWN_SECONDS).AddMilliseconds(VISUAL_COMPENSATION_MS);
                                countdownStarted = true;
                            }
                        }
                        catch (System.Exception ex) { LogError("FadeTimer", "动画异常", ex); fadeTimer.Stop(); UntrackTimer(fadeTimer); fadeTimer.Dispose(); }
                    };
                    fadeTimer.Start();
                }));
            };

            _hudTimer = new System.Windows.Forms.Timer();
            _hudTimer.Interval = HUD_TIMER_INTERVAL_MS;
            TrackTimer(_hudTimer);

            _hudTimer.Tick += delegate (object sender, EventArgs args)
            {
                try
                {
                    if (_hudForm == null || _hudForm.IsDisposed || !countdownStarted) return;

                    bool isOffScreen = (_hudForm.Location.X <= -1000 || _hudForm.Location.Y <= -1000);
                    TimeSpan remainingTime = isOffScreen ? TimeSpan.FromSeconds(COUNTDOWN_SECONDS) : (deadline - DateTime.Now);
                    if (isOffScreen) deadline = DateTime.Now.AddSeconds(COUNTDOWN_SECONDS);

                    int currentDisplaySecond = (int)Math.Ceiling(remainingTime.TotalSeconds);
                    IntPtr currentCadHandle = GetCadMainWindowHandle();
                    Point targetPos = CalculateHudTargetPosition(currentCadHandle, _hudForm.Width, _hudForm.Height);
                    if (_hudForm.Location != targetPos) _hudForm.Location = targetPos;

                    if (remainingTime.TotalSeconds <= 0)
                    {
                        DisposeHud();
                    }
                    else if (currentDisplaySecond != lastDisplayedSecond)
                    {
                        lastDisplayedSecond = currentDisplaySecond;
                        if (_hudCountdownLabel != null && !_hudCountdownLabel.IsDisposed)
                        {
                            _hudCountdownLabel.Text = "(" + currentDisplaySecond + "s 后自动关闭)";
                        }
                    }
                }
                catch (System.Exception ex) { LogError("HudTimer", "倒计时异常", ex); }
            };
            _hudTimer.Start();
        }
        #endregion

        #region 输入法切换核心逻辑 (🚨 本次修复重点区域)
        private void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            if (!_isPluginEnabled) return;
            try
            {
                string globalCmd = e.GlobalCommandName;

                // ✅ 修复：加入对“透明命令”的免疫（例如中途缩放 '_ZOOM）。带单引号的命令一律放行，不切换任何状态。
                if (!string.IsNullOrEmpty(globalCmd) && globalCmd.StartsWith("'")) return;

                string cmd = globalCmd.Trim().TrimStart('_', '-', '\'', '.');
                if (_chineseCommands.ContainsKey(cmd))
                {
                    SwitchIme(true);
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

                // ✅ 修复：透明命令结束时，不改变当前输入法状态
                if (!string.IsNullOrEmpty(globalCmd) && globalCmd.StartsWith("'")) return;

                // ✅ 修复：只要常规命令结束/取消（包括 customautoime 等任何命令），无条件强制切回英文。
                SwitchIme(false);
            }
            catch (System.Exception ex) { LogError("OnCommandEnded", "命令结束异常", ex); }
        }

        private void SwitchIme(bool open)
        {
            try
            {
                IntPtr focusHwnd = GetFocus();
                IntPtr himc = ImmGetContext(focusHwnd);

                // ✅ 修复：焦点穿透机制！
                // 如果当前抓到的系统焦点（例如刚新建图纸时的某一个悬浮面板）没有输入法上下文，
                // 直接强制提升级别，抓取 AutoCAD 主程序边框的输入法句柄进行切换。
                if (himc == IntPtr.Zero)
                {
                    focusHwnd = GetCadMainWindowHandle();
                    himc = ImmGetContext(focusHwnd);
                }

                if (himc != IntPtr.Zero)
                {
                    ImmSetOpenStatus(himc, open);
                    ImmReleaseContext(focusHwnd, himc); // 极度重要，防止句柄泄露
                }
            }
            catch { /* 必须静默吸收异常，绝对不能抛出导致 CAD 闪退 */ }
        }
        #endregion

        private static System.Drawing.Font CreateSafeFont(string name, float size, System.Drawing.FontStyle style)
        {
            try { return new System.Drawing.Font(name, size, style); }
            catch { return new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, size, style); }
        }
    }
}