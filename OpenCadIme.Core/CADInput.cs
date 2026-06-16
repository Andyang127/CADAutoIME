using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;

[assembly: ExtensionApplication(typeof(OpenCadIme.Switcher))]

namespace OpenCadIme
{
    public class Switcher : IExtensionApplication
    {
        [DllImport("imm32.dll")] public static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] public static extern bool ImmSetOpenStatus(IntPtr hIMC, bool b);
        [DllImport("imm32.dll")] public static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr hIMC);

        [DllImport("user32.dll")] static extern IntPtr GetFocus();

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private const string AppVersion = "0.2";
        private const int COUNTDOWN_SECONDS = 15;
        private const double VISUAL_COMPENSATION_MS = 300;

        private Dictionary<string, bool> chineseCommands;
        private List<Document> hookedDocs = new List<Document>();
        private List<Document> pendingWelcomeDocs = new List<Document>();

        private bool isPluginEnabled = true;

        private System.Windows.Forms.Form hudForm = null;
        private System.Windows.Forms.Label hudCountdownLabel = null;
        private System.Windows.Forms.Timer hudTimer = null;

        private void LogError(string msg)
        {
            try
            {
                if (Application.DocumentManager.MdiActiveDocument != null &&
                    Application.DocumentManager.MdiActiveDocument.Editor != null)
                {
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n>>> [浅醉·墨语]CAD Auto IME 输入法自动切换助手 错误: {msg} <<<\n");
                }
            }
            catch { }
        }

        public void Initialize()
        {
            try
            {
                chineseCommands = ConfigManager.LoadCommands();
                foreach (Document doc in Application.DocumentManager)
                {
                    AttachEvents(doc);
                }
                Application.DocumentManager.DocumentCreated += OnDocumentCreated;
                Application.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;

                // 🚀 终极兼容修复：绕过 CAD 的 API，直接调用 Windows 底层 UI 线程的 Idle 事件
                // 完美兼容 CAD 2007 (Sys17) 缺失该 API 的问题
                System.Windows.Forms.Application.Idle += OnApplicationIdle;
            }
            catch (System.Exception ex) { LogError($"初始化失败: {ex.Message}"); }
        }

        public void Terminate()
        {
            try
            {
                DisposeHud();
                Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;

                // 🚀 同步修改注销事件
                System.Windows.Forms.Application.Idle -= OnApplicationIdle;

                for (int i = hookedDocs.Count - 1; i >= 0; i--)
                {
                    if (hookedDocs[i] != null && !hookedDocs[i].IsDisposed)
                    {
                        DetachEvents(hookedDocs[i]);
                    }
                }
                hookedDocs.Clear();
                pendingWelcomeDocs.Clear();
            }
            catch (System.Exception ex) { LogError($"卸载失败: {ex.Message}"); }
        }

        private void OnApplicationIdle(object sender, EventArgs e)
        {
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && doc.Editor != null && pendingWelcomeDocs.Contains(doc))
                {
                    pendingWelcomeDocs.Remove(doc);
                    ShowWelcomeMessage(doc);
                }
            }
            catch { }
        }

        private void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null)
            {
                AttachEvents(e.Document);
            }
        }

        [CommandMethod("TOGGLEAUTOIME")]
        public void ToggleAutoImeCommand()
        {
            isPluginEnabled = !isPluginEnabled;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null && doc.Editor != null)
                doc.Editor.WriteMessage("\n>>> [浅醉·墨语] CAD Auto IME 输入法自动切换助手已 " + (isPluginEnabled ? "开启" : "关闭") + " <<<\n");
        }

        [CommandMethod("CUSTOMAUTOIME")]
        public void ShowCustomConfigDialog()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                using (var form = new ConfigForm())
                {
                    System.Windows.Forms.NativeWindow cadWin = new System.Windows.Forms.NativeWindow();
                    cadWin.AssignHandle(Application.MainWindow.Handle);

                    if (form.ShowDialog(cadWin) == System.Windows.Forms.DialogResult.OK)
                    {
                        chineseCommands = ConfigManager.LoadCommands();
                        if (doc != null && doc.Editor != null)
                            doc.Editor.WriteMessage($"\n>>> [浅醉·墨语] CAD Auto IME 白名单配置已更新，当前共加载 {ConfigManager.LoadedCustomCount} 个自定义命令，已立即生效！ <<<\n");
                    }
                }
            }
            catch (System.Exception ex) { LogError($"无法打开配置面板 - {ex.Message}"); }
        }

        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null) AttachEvents(e.Document);
        }

        private void CleanUpDeadDocuments()
        {
            for (int i = hookedDocs.Count - 1; i >= 0; i--)
            {
                if (hookedDocs[i] == null || hookedDocs[i].IsDisposed) hookedDocs.RemoveAt(i);
            }
            for (int i = pendingWelcomeDocs.Count - 1; i >= 0; i--)
            {
                if (pendingWelcomeDocs[i] == null || pendingWelcomeDocs[i].IsDisposed) pendingWelcomeDocs.RemoveAt(i);
            }
        }

        private void AttachEvents(Document doc)
        {
            if (doc != null)
            {
                CleanUpDeadDocuments();
                if (!hookedDocs.Contains(doc))
                {
                    doc.CommandWillStart += OnCommandWillStart;
                    doc.CommandEnded += OnCommandEnded;
                    doc.CommandCancelled += OnCommandEnded;
                    hookedDocs.Add(doc);

                    if (!pendingWelcomeDocs.Contains(doc)) pendingWelcomeDocs.Add(doc);
                }
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
            catch (System.Exception ex) { LogError($"事件解绑失败: {ex.Message}"); }
        }

        private void DisposeHud()
        {
            try
            {
                if (hudTimer != null) { hudTimer.Stop(); hudTimer.Dispose(); hudTimer = null; }
                if (hudCountdownLabel != null && !hudCountdownLabel.IsDisposed) { hudCountdownLabel.Dispose(); hudCountdownLabel = null; }
                if (hudForm != null && !hudForm.IsDisposed) { hudForm.Close(); hudForm.Dispose(); hudForm = null; }
            }
            catch { }
        }

        private Point CalculateHudTargetPosition(IntPtr cadHandle, int realWidth, int realHeight)
        {
            Point fallback = new Point(-5000, -5000);
            if (cadHandle == IntPtr.Zero) return fallback;

            RECT cadRect;
            if (!GetWindowRect(cadHandle, out cadRect)) return fallback;

            int cadWidth = cadRect.Right - cadRect.Left;
            int cadHeight = cadRect.Bottom - cadRect.Top;
            if (cadWidth <= 0 || cadHeight <= 0) return fallback;

            int targetX = cadRect.Left + (int)(cadWidth * 2.0 / 3.0);
            int targetY = cadRect.Top + (int)(cadHeight * 2.0 / 3.0);

            if (targetX + realWidth > cadRect.Right) targetX = cadRect.Right - realWidth - 10;
            if (targetY + realHeight > cadRect.Bottom) targetY = cadRect.Bottom - realHeight - 10;

            return new Point(targetX, targetY);
        }

        private void ShowWelcomeMessage(Document doc)
        {
            if (doc == null || doc.Editor == null) return;

            // 🚀 1. 命令行打印 (每次必定执行)
            doc.Editor.WriteMessage("\n====================================================\n");
            doc.Editor.WriteMessage("[[浅醉·墨语]CAD Auto IME 输入法自动切换程序] V" + AppVersion + " 已成功启动！\n");
            if (ConfigManager.LoadedCustomCount > 0) doc.Editor.WriteMessage(">>> 已成功从 AutoImeCommands.txt 载入 " + ConfigManager.LoadedCustomCount + " 个自定义白名单命令 <<<\n");
            doc.Editor.WriteMessage("----------------------------------------------------\n");
            doc.Editor.WriteMessage("💡 输入命令 TOGGLEAUTOIME 可开启/关闭本插件\n");
            doc.Editor.WriteMessage("💡 输入命令 CUSTOMAUTOIME 可调出白名单配置面板\n");
            doc.Editor.WriteMessage("====================================================\n");

            // 🚀 2. HUD 悬浮窗弹窗逻辑 (终生仅弹一次)
            string regPath = @"Software\QianZuiMoYu\CADAutoIme";
            string cadInternalVersion = Application.Version.Major.ToString() + "_" + Application.Version.Minor.ToString();
            string regMarker = "HasShownWelcome_V" + AppVersion + "_CAD" + cadInternalVersion;

            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath))
                {
                    if (key.GetValue(regMarker) != null) return;
                    key.SetValue(regMarker, 1);
                }
            }
            catch { return; }

            try
            {
                DisposeHud();

                hudForm = new System.Windows.Forms.Form();
                hudForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                hudForm.BackColor = Color.FromArgb(45, 52, 54);
                hudForm.TopMost = true;
                hudForm.ShowInTaskbar = false;
                hudForm.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                hudForm.Opacity = 0;
                hudForm.AutoSize = false;

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

                System.Windows.Forms.Label lblTitle = new System.Windows.Forms.Label
                {
                    Text = "CAD Auto IME 输入法自动切换助手",
                    AutoSize = true,
                    Font = new System.Drawing.Font("微软雅黑", 16, System.Drawing.FontStyle.Bold),
                    ForeColor = Color.White,
                    Anchor = System.Windows.Forms.AnchorStyles.Bottom
                };
                System.Windows.Forms.Label lblVer = new System.Windows.Forms.Label
                {
                    Text = "v" + AppVersion,
                    AutoSize = true,
                    Font = new System.Drawing.Font("Consolas", 12, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic),
                    ForeColor = Color.FromArgb(0, 210, 211),
                    Anchor = System.Windows.Forms.AnchorStyles.Bottom,
                    Margin = new System.Windows.Forms.Padding(6, 0, 0, 3)
                };
                titlePanel.Controls.Add(lblTitle);
                titlePanel.Controls.Add(lblVer);
                table.Controls.Add(titlePanel, 0, 0);
                table.SetColumnSpan(titlePanel, 2);

                System.Windows.Forms.Label lblSlogan = new System.Windows.Forms.Label
                {
                    Text = "让每一位设计师都能专注于创作，而非输入法切换",
                    AutoSize = true,
                    Font = new System.Drawing.Font("微软雅黑", 9.5f, System.Drawing.FontStyle.Regular),
                    ForeColor = Color.DarkGray,
                    Margin = new System.Windows.Forms.Padding(2, 5, 0, 15)
                };
                table.Controls.Add(lblSlogan, 0, 1);
                table.SetColumnSpan(lblSlogan, 2);

                System.Windows.Forms.Label lblOnceTip = new System.Windows.Forms.Label
                {
                    Text = "* 注：本欢迎视窗仅显示一次",
                    AutoSize = true,
                    Font = new System.Drawing.Font("微软雅黑", 9, System.Drawing.FontStyle.Regular),
                    ForeColor = Color.FromArgb(255, 82, 82),
                    Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom
                };
                hudCountdownLabel = new System.Windows.Forms.Label
                {
                    Text = $"( {COUNTDOWN_SECONDS}s 后自动关闭 )",
                    AutoSize = true,
                    Font = new System.Drawing.Font("微软雅黑", 9, System.Drawing.FontStyle.Regular),
                    ForeColor = Color.FromArgb(255, 165, 0),
                    Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom
                };
                table.Controls.Add(lblOnceTip, 0, 2);
                table.Controls.Add(hudCountdownLabel, 1, 2);

                hudForm.Controls.Add(table);

                table.PerformLayout();
                hudForm.Size = table.PreferredSize;

                hudForm.Paint += (s, e) => {
                    int realW = hudForm.Width;
                    int realH = hudForm.Height;
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        int r = 16;
                        path.AddArc(0, 0, r, r, 180, 90);
                        path.AddArc(realW - r - 1, 0, r, r, 270, 90);
                        path.AddArc(realW - r - 1, realH - r - 1, r, r, 0, 90);
                        path.AddArc(0, realH - r - 1, r, r, 90, 90);
                        path.CloseAllFigures();
                        using (Pen borderPen = new Pen(Color.FromArgb(90, 90, 90), 1.5f))
                        {
                            e.Graphics.DrawPath(borderPen, path);
                        }
                    }
                };

                int style = GetWindowLong(hudForm.Handle, GWL_EXSTYLE);
                SetWindowLong(hudForm.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);

                bool countdownStarted = false;
                DateTime deadline = DateTime.MaxValue;
                int lastDisplayedSecond = COUNTDOWN_SECONDS;

                hudForm.Shown += delegate (object sender, EventArgs e)
                {
                    hudForm.BeginInvoke(new Action(() =>
                    {
                        IntPtr hrgn = CreateRoundRectRgn(0, 0, hudForm.Width, hudForm.Height, 16, 16);
                        hudForm.Region = System.Drawing.Region.FromHrgn(hrgn);
                        DeleteObject(hrgn);

                        IntPtr cadHandle = Application.MainWindow.Handle;
                        Point targetPos = CalculateHudTargetPosition(cadHandle, hudForm.Width, hudForm.Height);
                        hudForm.Location = targetPos;

                        System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer { Interval = 15 };
                        fadeTimer.Tick += (s, a) =>
                        {
                            if (hudForm.Opacity < 0.95)
                                hudForm.Opacity += 0.12;
                            else
                            {
                                fadeTimer.Stop();
                                fadeTimer.Dispose();
                                deadline = DateTime.Now.AddSeconds(COUNTDOWN_SECONDS).AddMilliseconds(VISUAL_COMPENSATION_MS);
                                countdownStarted = true;
                            }
                        };
                        fadeTimer.Start();
                    }));
                };

                hudForm.Show();

                hudTimer = new System.Windows.Forms.Timer { Interval = 30 };
                hudTimer.Tick += delegate (object sender, EventArgs args)
                {
                    try
                    {
                        if (hudForm == null || hudForm.IsDisposed || !countdownStarted) return;

                        bool isOffScreen = (hudForm.Location.X <= -1000 || hudForm.Location.Y <= -1000);
                        TimeSpan remainingTime = isOffScreen ? TimeSpan.FromSeconds(COUNTDOWN_SECONDS) : (deadline - DateTime.Now);
                        if (isOffScreen) deadline = DateTime.Now.AddSeconds(COUNTDOWN_SECONDS);

                        int currentDisplaySecond = (int)Math.Ceiling(remainingTime.TotalSeconds);

                        IntPtr currentCadHandle = Application.MainWindow.Handle;
                        Point targetPos = CalculateHudTargetPosition(currentCadHandle, hudForm.Width, hudForm.Height);
                        if (hudForm.Location != targetPos) hudForm.Location = targetPos;

                        if (remainingTime.TotalSeconds <= 0)
                        {
                            DisposeHud();
                        }
                        else if (currentDisplaySecond != lastDisplayedSecond)
                        {
                            lastDisplayedSecond = currentDisplaySecond;
                            if (hudCountdownLabel != null && !hudCountdownLabel.IsDisposed)
                            {
                                hudCountdownLabel.Text = $"( {currentDisplaySecond}s 后自动关闭 )";
                            }
                        }
                    }
                    catch { }
                };
                hudTimer.Start();
            }
            catch (System.Exception ex)
            {
                LogError($"HUD 悬浮窗加载失败: {ex.Message}");
            }
        }

        private void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            if (!isPluginEnabled) return;
            try
            {
                if (string.IsNullOrEmpty(e.GlobalCommandName)) return;
                string cmd = e.GlobalCommandName.Trim().TrimStart('_', '-', '\'').ToUpper();
                bool isTextCommand = chineseCommands.ContainsKey(cmd);
                TriggerIme(isTextCommand);
            }
            catch (System.Exception ex) { LogError($"命令拦截异常: {ex.Message}"); }
        }

        private void OnCommandEnded(object sender, CommandEventArgs e)
        {
            if (!isPluginEnabled) return;
            TriggerIme(false);
        }

        private void TriggerIme(bool enable)
        {
            try
            {
                IntPtr targetHwnd = GetFocus();
                if (targetHwnd == IntPtr.Zero) targetHwnd = Application.MainWindow.Handle;
                if (targetHwnd == IntPtr.Zero) return;

                System.Windows.Forms.Timer deferTimer = new System.Windows.Forms.Timer();
                deferTimer.Interval = 50;
                deferTimer.Tick += delegate (object sender, EventArgs args)
                {
                    deferTimer.Stop();
                    deferTimer.Dispose();
                    try
                    {
                        IntPtr himc = ImmGetContext(targetHwnd);
                        if (himc != IntPtr.Zero)
                        {
                            ImmSetOpenStatus(himc, enable);
                            ImmReleaseContext(targetHwnd, himc);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("CAD Auto IME 输入法自动切换助手 安全延时线程异常: " + ex.Message);
                    }
                };
                deferTimer.Start();
            }
            catch (System.Exception ex) { LogError($"输入法切换触发失败: {ex.Message}"); }
        }
    }
}