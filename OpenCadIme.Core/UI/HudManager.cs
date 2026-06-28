#pragma warning disable CA1416
using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCadIme.Interop;

// 解决 Application 歧义：别名区分 CAD 和 WinForms
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsApp = System.Windows.Forms.Application;

namespace OpenCadIme.UI
{
    public class HudManager : IDisposable
    {
        private const int COUNTDOWN_SECONDS = 15;
        private const double VISUAL_COMPENSATION_MS = 300;
        private const int HUD_FADE_INTERVAL_MS = 15;
        private const double HUD_FADE_INCREMENT = 0.12;
        private const int HUD_TIMER_INTERVAL_MS = 100;

        private HudForm _hudForm;
        private Label _hudCountdownLabel;
        private Timer _hudTimer;
        private Timer _fadeTimer;
        private bool _disposed = false;
        private Font _fontTitle;
        private Font _fontVer;
        private Font _fontSlogan;
        private Font _fontOnceTip;
        private Font _fontCountdown;

        private IntPtr _cachedCadHandle = IntPtr.Zero;

        private class HudForm : Form
        {
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    // 0x08000000 = WS_EX_NOACTIVATE (防抢焦点)
                    // 0x00080000 = WS_EX_LAYERED (分层透明)
                    // 0x00000020 = WS_EX_TRANSPARENT (鼠标穿透)
                    cp.ExStyle |= 0x08000000 | 0x00080000 | 0x00000020;
                    return cp;
                }
            }
        }

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
            doc.Editor.WriteMessage("[[浅醉·墨语] CAD Auto IME 输入法自动切换程序] V" + appVersion + " 已成功启动！\n");
            if (OpenCadIme.Core.ConfigManager.LoadedCustomCount > 0)
                doc.Editor.WriteMessage(">>> 已成功从 AutoImeCommands.txt 载入 " + OpenCadIme.Core.ConfigManager.LoadedCustomCount + " 个自定义白名单命令 <<<\n");
            doc.Editor.WriteMessage("----------------------------------------------------\n");
            doc.Editor.WriteMessage("💡 输入命令 TOGGLEAUTOIME 可开启/关闭本插件\n");
            doc.Editor.WriteMessage("💡 输入命令 CUSTOMAUTOIME 可调出白名单配置面板\n");
            doc.Editor.WriteMessage("====================================================\n");

            // 【终极修复】：删除了此处的冗余且冲突的注册表写入逻辑，状态判断全权交由 PluginMain 处理。
            ShowHudWelcomeWindow(appVersion);
        }

        private void ShowHudWelcomeWindow(string appVersion)
        {
            try
            {
                DisposeHud();

                _hudForm = new HudForm();
                _hudForm.FormBorderStyle = FormBorderStyle.None;
                _hudForm.BackColor = Color.FromArgb(45, 52, 54);
                _hudForm.TopMost = true;
                _hudForm.ShowInTaskbar = false;
                _hudForm.StartPosition = FormStartPosition.Manual;
                _hudForm.Opacity = 0;
                _hudForm.AutoSize = false;

                TableLayoutPanel table = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.Transparent,
                    Padding = new Padding(20, 15, 20, 15),
                    ColumnCount = 2,
                    RowCount = 3
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                FlowLayoutPanel titlePanel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    Margin = new Padding(0)
                };
                titlePanel.Controls.Add(new Label
                {
                    Text = "CAD Auto IME 输入法自动切换",
                    AutoSize = true,
                    Font = _fontTitle,
                    ForeColor = Color.White,
                    Anchor = AnchorStyles.Bottom
                });
                titlePanel.Controls.Add(new Label
                {
                    Text = "v" + appVersion,
                    AutoSize = true,
                    Font = _fontVer,
                    ForeColor = Color.FromArgb(0, 210, 211),
                    Anchor = AnchorStyles.Bottom,
                    Margin = new Padding(6, 0, 0, 3)
                });

                table.Controls.Add(titlePanel, 0, 0);
                table.SetColumnSpan(titlePanel, 2);

                Label lblSlogan = new Label
                {
                    Text = "让每一位设计师都能专注于创作，而非输入法切换",
                    AutoSize = true,
                    Font = _fontSlogan,
                    ForeColor = Color.DarkGray,
                    Margin = new Padding(2, 5, 0, 15)
                };
                table.Controls.Add(lblSlogan, 0, 1);
                table.SetColumnSpan(lblSlogan, 2);

                table.Controls.Add(new Label
                {
                    Text = "* 注：本提示只在初次配置该 CAD 版本时显示一次",
                    AutoSize = true,
                    Font = _fontOnceTip,
                    ForeColor = Color.FromArgb(255, 82, 82),
                    Anchor = AnchorStyles.Left | AnchorStyles.Bottom
                }, 0, 2);

                _hudCountdownLabel = new Label
                {
                    Text = $"({COUNTDOWN_SECONDS}s 后自动关闭)",
                    AutoSize = true,
                    Font = _fontCountdown,
                    ForeColor = Color.FromArgb(255, 165, 0),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                table.Controls.Add(_hudCountdownLabel, 1, 2);

                _hudForm.Controls.Add(table);
                table.PerformLayout();
                _hudForm.Size = table.PreferredSize;

                _hudForm.Paint += HudForm_Paint;

                SetupHudTimers();

                _hudForm.Show();
            }
            catch { /* 静默失败 */ }
        }

        private void HudForm_Paint(object sender, PaintEventArgs e)
        {
            if (_hudForm == null) return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                int r = 16, w = _hudForm.Width - 1, h = _hudForm.Height - 1;
                path.AddArc(0, 0, r, r, 180, 90);
                path.AddArc(w - r, 0, r, r, 270, 90);
                path.AddArc(w - r, h - r, r, r, 0, 90);
                path.AddArc(0, h - r, r, r, 90, 90);
                path.CloseAllFigures();
                using (Pen pen = new Pen(Color.FromArgb(90, 90, 90), 1.5f))
                    e.Graphics.DrawPath(pen, path);
            }
        }

        private void SetupHudTimers()
        {
            bool countdownStarted = false;
            DateTime deadline = DateTime.MaxValue;
            int lastDisplayedSecond = COUNTDOWN_SECONDS;

            _hudForm.Shown += delegate (object sender, EventArgs e)
            {
                _hudForm.BeginInvoke(new MethodInvoker(delegate
                {
                    IntPtr hrgn = Win32API.CreateRoundRectRgn(0, 0, _hudForm.Width, _hudForm.Height, 16, 16);
                    Region oldRegion = _hudForm.Region;
                    _hudForm.Region = Region.FromHrgn(hrgn);
                    if (oldRegion != null) oldRegion.Dispose();
                    Win32API.DeleteObject(hrgn);

                    IntPtr cadHandle = GetCadMainWindowHandle();
                    Point targetPos = CalculateHudTargetPosition(cadHandle, _hudForm.Width, _hudForm.Height);
                    _hudForm.Location = targetPos;

                    _fadeTimer = new Timer();
                    _fadeTimer.Interval = HUD_FADE_INTERVAL_MS;
                    _fadeTimer.Tick += delegate (object s, EventArgs a)
                    {
                        try
                        {
                            if (_hudForm == null || _hudForm.IsDisposed)
                            {
                                StopAndDisposeTimer(ref _fadeTimer);
                                return;
                            }
                            if (_hudForm.Opacity < 0.95)
                            {
                                _hudForm.Opacity += HUD_FADE_INCREMENT;
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

            _hudTimer = new Timer();
            _hudTimer.Interval = HUD_TIMER_INTERVAL_MS;
            _hudTimer.Tick += delegate (object sender, EventArgs args)
            {
                try
                {
                    if (_hudForm == null || _hudForm.IsDisposed || !countdownStarted) return;

                    bool isOffScreen = (_hudForm.Location.X <= -1000 || _hudForm.Location.Y <= -1000);
                    TimeSpan remainingTime = isOffScreen
                        ? TimeSpan.FromSeconds(COUNTDOWN_SECONDS)
                        : (deadline - DateTime.Now);

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
                            _hudCountdownLabel.Text = $"({currentDisplaySecond}s 后自动关闭)";
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

            if (!Win32API.GetWindowRect(cadHandle, out Win32API.RECT cadRect))
                return new Point(fallbackX, fallbackY);

            int cadWidth = cadRect.Right - cadRect.Left;
            int cadHeight = cadRect.Bottom - cadRect.Top;
            if (cadWidth <= 0 || cadHeight <= 0) return new Point(fallbackX, fallbackY);

            int targetX = cadRect.Left + (int)(cadWidth * 2.0 / 3.0);
            int targetY = cadRect.Top + (int)(cadHeight * 2.0 / 3.0);

            if (targetX + realWidth > cadRect.Right) targetX = cadRect.Right - realWidth - 10;
            if (targetY + realHeight > cadRect.Bottom) targetY = cadRect.Bottom - realHeight - 10;

            return new Point(targetX, targetY);
        }

        private void StopAndDisposeTimer(ref Timer t)
        {
            if (t != null) { t.Stop(); t.Dispose(); t = null; }
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
                Region oldRegion = _hudForm.Region;
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

        private static Font CreateSafeFont(string name, float size, FontStyle style)
        {
            try { return new Font(name, size, style); }
            catch { return new Font(FontFamily.GenericSansSerif, size, style); }
        }

        private IntPtr GetCadMainWindowHandle()
        {
            if (_cachedCadHandle != IntPtr.Zero) return _cachedCadHandle;
            try
            {
                if (CadApp.MainWindow != null && CadApp.MainWindow.Handle != IntPtr.Zero)
                {
                    _cachedCadHandle = CadApp.MainWindow.Handle;
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
    }
}