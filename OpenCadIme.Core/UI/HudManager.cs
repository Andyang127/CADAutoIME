using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;

namespace OpenCadIme
{
    /// <summary>
    /// HUD 视觉管理器
    /// 专职处理启动时的欢迎信息与渐变悬浮窗，与输入法核心逻辑完全解耦
    /// </summary>
    internal class HudManager : IDisposable
    {
        private const int COUNTDOWN_SECONDS = 15;
        private const double VISUAL_COMPENSATION_MS = 300;
        private const int HUD_FADE_INTERVAL_MS = 15;
        private const double HUD_FADE_INCREMENT = 0.12;
        private const int HUD_TIMER_INTERVAL_MS = 30;
        private const string REGISTRY_PATH = @"Software\QianZuiMoYu\CADAutoIme";

        private System.Windows.Forms.Form _hudForm;
        private System.Windows.Forms.Label _hudCountdownLabel;
        private System.Windows.Forms.Timer _hudTimer;
        private System.Windows.Forms.Timer _fadeTimer;
        private bool _disposed = false;

        private Font _fontTitle;
        private Font _fontVer;
        private Font _fontSlogan;
        private Font _fontOnceTip;
        private Font _fontCountdown;

        public HudManager()
        {
            _fontTitle = CreateSafeFont("微软雅黑", 16, FontStyle.Bold);
            _fontVer = CreateSafeFont("Consolas", 12, FontStyle.Bold | FontStyle.Italic);
            _fontSlogan = CreateSafeFont("微软雅黑", 9.5f, FontStyle.Regular);
            _fontOnceTip = CreateSafeFont("微软雅黑", 9, FontStyle.Regular);
            _fontCountdown = CreateSafeFont("微软雅黑", 9, FontStyle.Regular);
        }

        public void ShowWelcomeMessage(Document doc, string appVersion)
        {
            if (doc == null || doc.Editor == null) return;

            doc.Editor.WriteMessage("\n====================================================\n");
            doc.Editor.WriteMessage("[[浅醉·墨语]CAD Auto IME 输入法自动切换程序] V" + appVersion + " 已成功启动！\n");
            if (ConfigManager.LoadedCustomCount > 0)
                doc.Editor.WriteMessage(">>> 已成功从 AutoImeCommands.txt 载入 " + ConfigManager.LoadedCustomCount + " 个自定义白名单命令 <<<\n");
            doc.Editor.WriteMessage("----------------------------------------------------\n");
            doc.Editor.WriteMessage("💡 输入命令 TOGGLEAUTOIME 可开启/关闭本插件\n");
            doc.Editor.WriteMessage("💡 输入命令 CUSTOMAUTOIME 可调出白名单配置面板\n");
            doc.Editor.WriteMessage("====================================================\n");

            string cadInternalVersion = Application.Version.Major.ToString() + "_" + Application.Version.Minor.ToString();
            string regMarker = "HasShownWelcome_V" + appVersion + "_CAD" + cadInternalVersion;

            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    if (key.GetValue(regMarker) != null) return;
                    key.SetValue(regMarker, 1);
                }
            }
            catch { /* 静默失败 */ }

            ShowHudWelcomeWindow(appVersion);
        }

        private void ShowHudWelcomeWindow(string appVersion)
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
                lblVer.Text = "v" + appVersion;
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
                lblSlogan.Text = "让每一位设计师都能专注于创作，而非输入法切换";
                lblSlogan.AutoSize = true;
                lblSlogan.Font = _fontSlogan;
                lblSlogan.ForeColor = Color.DarkGray;
                lblSlogan.Margin = new System.Windows.Forms.Padding(2, 5, 0, 15);
                table.Controls.Add(lblSlogan, 0, 1);
                table.SetColumnSpan(lblSlogan, 2);

                System.Windows.Forms.Label lblOnceTip = new System.Windows.Forms.Label();
                lblOnceTip.Text = "* 注：本提示只在第一配置CAD版本时显示一次";
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

                int style = Win32API.GetWindowLong(_hudForm.Handle, Win32API.GWL_EXSTYLE);
                Win32API.SetWindowLong(_hudForm.Handle, Win32API.GWL_EXSTYLE, style | Win32API.WS_EX_LAYERED | Win32API.WS_EX_TRANSPARENT);

                SetupHudTimers();
                _hudForm.Show();
            }
            catch (System.Exception) { /* 静默失败，不影响主流程 */ }
        }

        private void HudForm_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            if (_hudForm == null) return;
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
                    IntPtr hrgn = Win32API.CreateRoundRectRgn(0, 0, _hudForm.Width, _hudForm.Height, 16, 16);
                    System.Drawing.Region oldRegion = _hudForm.Region;
                    _hudForm.Region = System.Drawing.Region.FromHrgn(hrgn);
                    if (oldRegion != null) oldRegion.Dispose();
                    Win32API.DeleteObject(hrgn);

                    IntPtr cadHandle = GetCadMainWindowHandle();
                    Point targetPos = CalculateHudTargetPosition(cadHandle, _hudForm.Width, _hudForm.Height);
                    _hudForm.Location = targetPos;

                    _fadeTimer = new System.Windows.Forms.Timer();
                    _fadeTimer.Interval = HUD_FADE_INTERVAL_MS;

                    _fadeTimer.Tick += delegate (object s, EventArgs a)
                    {
                        try
                        {
                            if (_hudForm == null || _hudForm.IsDisposed)
                            {
                                StopAndDisposeTimer(ref _fadeTimer); return;
                            }
                            if (_hudForm.Opacity < 0.95)
                            {
                                _hudForm.Opacity = _hudForm.Opacity + HUD_FADE_INCREMENT;
                            }
                            else
                            {
                                StopAndDisposeTimer(ref _fadeTimer);
                                deadline = DateTime.Now.AddSeconds(COUNTDOWN_SECONDS).AddMilliseconds(VISUAL_COMPENSATION_MS);
                                countdownStarted = true;
                            }
                        }
                        catch { StopAndDisposeTimer(ref _fadeTimer); }
                    };
                    _fadeTimer.Start();
                }));
            };

            _hudTimer = new System.Windows.Forms.Timer();
            _hudTimer.Interval = HUD_TIMER_INTERVAL_MS;

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
                catch { }
            };
            _hudTimer.Start();
        }

        private Point CalculateHudTargetPosition(IntPtr cadHandle, int realWidth, int realHeight)
        {
            const int fallbackX = -5000;
            const int fallbackY = -5000;
            if (cadHandle == IntPtr.Zero) return new Point(fallbackX, fallbackY);

            Win32API.RECT cadRect;
            if (!Win32API.GetWindowRect(cadHandle, out cadRect)) return new Point(fallbackX, fallbackY);

            int cadWidth = cadRect.Right - cadRect.Left;
            int cadHeight = cadRect.Bottom - cadRect.Top;
            if (cadWidth <= 0 || cadHeight <= 0) return new Point(fallbackX, fallbackY);

            int targetX = cadRect.Left + (int)(cadWidth * 2.0 / 3.0);
            int targetY = cadRect.Top + (int)(cadHeight * 2.0 / 3.0);

            if (targetX + realWidth > cadRect.Right) targetX = cadRect.Right - realWidth - 10;
            if (targetY + realHeight > cadRect.Bottom) targetY = cadRect.Bottom - realHeight - 10;

            return new Point(targetX, targetY);
        }

        private void StopAndDisposeTimer(ref System.Windows.Forms.Timer t)
        {
            if (t != null)
            {
                t.Stop();
                t.Dispose();
                t = null;
            }
        }

        private void DisposeHud()
        {
            StopAndDisposeTimer(ref _hudTimer);
            StopAndDisposeTimer(ref _fadeTimer);

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

        public void Dispose()
        {
            if (_disposed) return;
            DisposeHud();
            if (_fontTitle != null) { _fontTitle.Dispose(); _fontTitle = null; }
            if (_fontVer != null) { _fontVer.Dispose(); _fontVer = null; }
            if (_fontSlogan != null) { _fontSlogan.Dispose(); _fontSlogan = null; }
            if (_fontOnceTip != null) { _fontOnceTip.Dispose(); _fontOnceTip = null; }
            if (_fontCountdown != null) { _fontCountdown.Dispose(); _fontCountdown = null; }
            _disposed = true;
        }

        private static System.Drawing.Font CreateSafeFont(string name, float size, System.Drawing.FontStyle style)
        {
            try { return new System.Drawing.Font(name, size, style); }
            catch { return new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, size, style); }
        }

        private static IntPtr GetCadMainWindowHandle()
        {
            try { return Application.MainWindow.Handle; }
            catch { return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
        }
    }
}