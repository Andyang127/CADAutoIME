#pragma warning disable CA1416
using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCadIme.Interop;

using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsApp = System.Windows.Forms.Application;

namespace OpenCadIme.UI
{
    public class HudManager : IDisposable
    {
        private const int COUNTDOWN_SECONDS = 6;
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
        private Font _fontClose;

        private IntPtr _cachedCadHandle = IntPtr.Zero;

        private class HudForm : Form
        {
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    // WS_EX_NOACTIVATE (0x08000000): 不抢占 CAD 主窗口焦点
                    // WS_EX_LAYERED (0x00080000): 支持透明度渐变动画
                    // 不加 WS_EX_TRANSPARENT (0x00000020)，允许用户点击关闭
                    cp.ExStyle |= 0x08000000 | 0x00080000;
                    return cp;
                }
            }

            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }
        }

        public HudManager()
        {
            _fontTitle = CreateSafeFont("微软雅黑", 14, FontStyle.Bold);
            _fontVer = CreateSafeFont("Consolas", 11, FontStyle.Bold | FontStyle.Italic);
            _fontSlogan = CreateSafeFont("微软雅黑", 9.5f, FontStyle.Regular);
            _fontOnceTip = CreateSafeFont("微软雅黑", 9, FontStyle.Regular);
            _fontCountdown = CreateSafeFont("微软雅黑", 9, FontStyle.Regular);
            _fontClose = CreateSafeFont("微软雅黑", 10, FontStyle.Regular);
        }

        public void ShowWelcomeMessage(Document doc, string appVersion)
        {
            if (doc == null || doc.Editor == null) return;

            doc.Editor.WriteMessage("\n====================================================\n");
            doc.Editor.WriteMessage("[墨语 X - CAD Auto IME] v" + appVersion + " 已成功启动！\n");
            if (OpenCadIme.Core.ConfigManager.LoadedCustomCount > 0)
                doc.Editor.WriteMessage(">>> 已成功从 AutoImeCommands.txt 载入 " + OpenCadIme.Core.ConfigManager.LoadedCustomCount + " 个自定义白名单命令 <<<\n");
            doc.Editor.WriteMessage("----------------------------------------------------\n");
            doc.Editor.WriteMessage("💡 输入命令 TOGGLEAUTOIME 可开启/关闭本插件\n");
            doc.Editor.WriteMessage("💡 输入命令 CUSTOMAUTOIME 可调出白名单配置面板\n");
            doc.Editor.WriteMessage("====================================================\n");

            ShowHudWelcomeWindow(appVersion);
        }

        public void DismissQuick()
        {
            try
            {
                if (_hudForm != null && !_hudForm.IsDisposed)
                {
                    DisposeHud();
                }
            }
            catch { }
        }

        private void ShowHudWelcomeWindow(string appVersion)
        {
            try
            {
                DisposeHud();

                _hudForm = new HudForm();
                _hudForm.FormBorderStyle = FormBorderStyle.None;
                // 使用极清截图统一暗黑背景色 #2D2D2D，不再使用 TransparencyKey 产生黑色锯齿边缘
                _hudForm.BackColor = Color.FromArgb(45, 45, 45);
                _hudForm.TopMost = true;
                _hudForm.ShowInTaskbar = false;
                _hudForm.StartPosition = FormStartPosition.Manual;
                _hudForm.Opacity = 0;
                _hudForm.AutoSize = false;
                _hudForm.Cursor = Cursors.Default;

                TableLayoutPanel table = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.FromArgb(45, 45, 45),
                    Padding = new Padding(18, 14, 18, 14),
                    ColumnCount = 3,
                    RowCount = 3
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                // 标题栏面板
                FlowLayoutPanel titlePanel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    Margin = new Padding(0)
                };
                titlePanel.Controls.Add(new Label
                {
                    Text = "墨语 X - CAD Auto IME",
                    AutoSize = true,
                    Font = _fontTitle,
                    ForeColor = Color.FromArgb(245, 245, 245),
                    Anchor = AnchorStyles.Bottom
                });
                titlePanel.Controls.Add(new Label
                {
                    Text = "v" + appVersion,
                    AutoSize = true,
                    Font = _fontVer,
                    ForeColor = Color.FromArgb(0, 122, 204), // 极清截图科技蓝
                    Anchor = AnchorStyles.Bottom,
                    Margin = new Padding(6, 0, 0, 2)
                });

                table.Controls.Add(titlePanel, 0, 0);
                table.SetColumnSpan(titlePanel, 2);

                // 右上角快速关闭按钮 ✕
                Label btnClose = new Label
                {
                    Text = "✕",
                    AutoSize = true,
                    Font = _fontClose,
                    ForeColor = Color.FromArgb(140, 140, 140),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Margin = new Padding(8, 0, 0, 0)
                };
                btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.FromArgb(232, 17, 35);
                btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(140, 140, 140);
                btnClose.Click += (s, e) => DismissQuick();
                table.Controls.Add(btnClose, 2, 0);

                // 副标题 Slogan
                Label lblSlogan = new Label
                {
                    Text = "输入法智能感知切换 · 专注于设计，而非输入法切换",
                    AutoSize = true,
                    Font = _fontSlogan,
                    ForeColor = Color.FromArgb(180, 180, 180),
                    Margin = new Padding(2, 6, 0, 14)
                };
                table.Controls.Add(lblSlogan, 0, 1);
                table.SetColumnSpan(lblSlogan, 3);

                // 底部提示
                Label lblOnceTip = new Label
                {
                    Text = "* 注：本提示仅初次加载时显示一次，点击可随时关闭",
                    AutoSize = true,
                    Font = _fontOnceTip,
                    ForeColor = Color.FromArgb(140, 140, 140),
                    Anchor = AnchorStyles.Left | AnchorStyles.Bottom
                };
                table.Controls.Add(lblOnceTip, 0, 2);
                table.SetColumnSpan(lblOnceTip, 2);

                _hudCountdownLabel = new Label
                {
                    Text = $"({COUNTDOWN_SECONDS}s 关闭)",
                    AutoSize = true,
                    Font = _fontCountdown,
                    ForeColor = Color.FromArgb(0, 122, 204),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                table.Controls.Add(_hudCountdownLabel, 2, 2);

                _hudForm.Controls.Add(table);
                table.PerformLayout();
                _hudForm.Size = table.PreferredSize;

                // 应用高质量圆角剪裁，彻底杜绝黑色锯齿边缘
                ApplyRoundedRegion(_hudForm.Width, _hudForm.Height);

                _hudForm.Paint += HudForm_Paint;

                // 点击 HUD 任意区域可直接关闭
                _hudForm.Click += (s, e) => DismissQuick();
                table.Click += (s, e) => DismissQuick();
                lblSlogan.Click += (s, e) => DismissQuick();
                lblOnceTip.Click += (s, e) => DismissQuick();

                SetupHudTimers();

                _hudForm.Show();
            }
            catch { /* 静默失败 */ }
        }

        private void ApplyRoundedRegion(int width, int height)
        {
            if (_hudForm == null || _hudForm.IsDisposed || width <= 0 || height <= 0) return;
            try
            {
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int r = 16;
                    int w = width - 1;
                    int h = height - 1;
                    path.AddArc(0, 0, r, r, 180, 90);
                    path.AddArc(w - r, 0, r, r, 270, 90);
                    path.AddArc(w - r, h - r, r, r, 0, 90);
                    path.AddArc(0, h - r, r, r, 90, 90);
                    path.CloseAllFigures();

                    Region oldRegion = _hudForm.Region;
                    _hudForm.Region = new Region(path);
                    if (oldRegion != null) oldRegion.Dispose();
                }
            }
            catch { }
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

                // 极清截图统一边框线 #555555
                using (Pen pen = new Pen(Color.FromArgb(85, 85, 85), 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
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
                _hudForm.BeginInvoke(new MethodInvoker(delegate
                {
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
                            _hudCountdownLabel.Text = $"({currentDisplaySecond}s 关闭)";
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
            if (cadWidth <= 100 || cadHeight <= 100) return new Point(fallbackX, fallbackY);

            // 右下角悬浮放置，去除错误重叠乘法 DPI 缩放，直接使用控件真实像素尺寸
            int margin = 24;
            int targetX = cadRect.Right - realWidth - margin;
            int targetY = cadRect.Bottom - realHeight - 40;

            if (targetX < cadRect.Left + 10) targetX = cadRect.Left + 10;
            if (targetY < cadRect.Top + 10) targetY = cadRect.Top + 10;

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
            if (_fontClose != null) { _fontClose.Dispose(); _fontClose = null; }
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