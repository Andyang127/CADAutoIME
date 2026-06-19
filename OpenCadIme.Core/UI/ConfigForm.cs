using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace OpenCadIme
{
    public partial class ConfigForm : Form
    {
        #region Win32 API：无边框拖拽 & 暗黑滚动条
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);
        #endregion

        #region 现代化暗黑主题配色
        private static readonly Color ThemeBgBase = Color.FromArgb(30, 30, 30);
        private static readonly Color ThemeBgSurface = Color.FromArgb(37, 37, 38);
        private static readonly Color ThemeBgInput = Color.FromArgb(45, 45, 48);
        private static readonly Color ThemeCyan = Color.FromArgb(0, 210, 211);
        private static readonly Color ThemeTextMain = Color.FromArgb(240, 240, 240);
        private static readonly Color ThemeTextMuted = Color.FromArgb(150, 150, 150);
        private static readonly Color ThemeDanger = Color.FromArgb(240, 71, 71);
        #endregion

        #region 字体设计
        private Font FontTitle;
        private Font FontSubtitle;
        private Font FontInput;
        private Font FontChip;
        private Font FontNormal;
        #endregion

        #region 业务数据字段
        private List<string> customCmds = new List<string>();
        private List<string> coreCmds = new List<string>();
        private Dictionary<string, bool> customCmdSet = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, bool> coreCmdSet = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private class UndoAction { public string Command { get; set; } public bool IsCore { get; set; } public int OriginalIndex { get; set; } }
        private Stack<UndoAction> undoStack = new Stack<UndoAction>();
        private Stack<UndoAction> redoStack = new Stack<UndoAction>();

        private FlowLayoutPanel flpCustom, flpCore;
        private TextBox txtNewCmd, txtSearch;
        private Label selectedCustomTag = null;
        private Label selectedCoreTag = null;

        private const string SearchPlaceholder = "🔍 搜索你需要管理的指令...";
        private const string AddPlaceholder = "输入指令 (例如: LINE, C, REC)...";
        #endregion

        public ConfigForm()
        {
            FontTitle = CreateSafeFont("微软雅黑", 15F, FontStyle.Bold);
            FontSubtitle = CreateSafeFont("Consolas", 10F, FontStyle.Italic);
            FontInput = CreateSafeFont("Consolas", 11F, FontStyle.Regular);
            FontChip = CreateSafeFont("Consolas", 9.5F, FontStyle.Regular);
            FontNormal = CreateSafeFont("微软雅黑", 9F, FontStyle.Regular);

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            // ✅ 整体加高到 720，为标题栏和底部栏留足空间
            this.ClientSize = new Size(620, 720);
            this.BackColor = ThemeBgBase;
            this.ForeColor = ThemeTextMain;

            SetRoundRegion();
            InitializeModernComponent();

            this.Shown += delegate { SetRoundRegion(); this.ActiveControl = null; };
            this.KeyPreview = true;
            this.KeyDown += Form_KeyDown;
        }

        private void SetRoundRegion()
        {
            try
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    int r = 16;
                    path.AddArc(0, 0, r, r, 180, 90);
                    path.AddArc(this.Width - r, 0, r, r, 270, 90);
                    path.AddArc(this.Width - r, this.Height - r, r, r, 0, 90);
                    path.AddArc(0, this.Height - r, r, r, 90, 90);
                    path.CloseAllFigures();
                    Region old = this.Region;
                    this.Region = new Region(path);
                    if (old != null) old.Dispose();
                }
            }
            catch { }
        }

        #region 全新现代化 UI 组装
        private void InitializeModernComponent()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(20, 15, 20, 15)
            };
            // ✅ 给 Header 留出更充足的 90px 高度，确保标题与副标题拥有充足的呼吸空间
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            // ✅ 修复底部遮挡：给 Footer 留出 90px 的高度
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            this.Controls.Add(mainLayout);

            // ================= 1. 顶部 Header =================
            Panel pnlHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            pnlHeader.MouseDown += Header_MouseDown;

            // ✅ 彻底解决副标题消失问题：引入垂直流式布局，系统自动堆叠，杜绝互相遮挡
            FlowLayoutPanel pnlTextVerticalFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Location = new Point(0, 5), WrapContents = false };
            pnlTextVerticalFlow.MouseDown += Header_MouseDown;

            FlowLayoutPanel pnlTitleFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0), WrapContents = false };
            pnlTitleFlow.MouseDown += Header_MouseDown;

            // ✅ 增加 Anchor = AnchorStyles.Bottom 使得主标题向底部对齐
            Label lblTitle = new Label { Text = "CAD Auto IME 自定义配置", Font = FontTitle, ForeColor = Color.White, AutoSize = true, Margin = new Padding(0), Anchor = AnchorStyles.Bottom };
            lblTitle.MouseDown += Header_MouseDown;

            // ✅ 增加 Anchor = AnchorStyles.Bottom 使其与主标题底边对齐，并微调 Margin 使其处于完美的右下角视觉位置
            Label lblVer = new Label { Text = "v0.2.1", Font = FontSubtitle, ForeColor = ThemeCyan, AutoSize = true, Margin = new Padding(4, 0, 0, 3), Anchor = AnchorStyles.Bottom };
            lblVer.MouseDown += Header_MouseDown;

            // ✅ 修复：必须把主标题和版本号加进横向容器里
            pnlTitleFlow.Controls.Add(lblTitle);
            pnlTitleFlow.Controls.Add(lblVer);

            // ✅ 将 20,20,20 修改为可见度更高的亮灰色，使其在深色背景中能够被肉眼看清
            Label lblSlogan = new Label { Text = "让每一位设计师都专注于创作，而非输入法切换", Font = FontNormal, ForeColor = Color.FromArgb(170, 170, 170), AutoSize = true, Margin = new Padding(2, 6, 0, 0) };
            lblSlogan.MouseDown += Header_MouseDown;

            // 标题+版本号 在第一排；口号 在第二排，绝对不会互相遮挡
            pnlTextVerticalFlow.Controls.Add(pnlTitleFlow);
            pnlTextVerticalFlow.Controls.Add(lblSlogan);

            Button btnClose = new Button { Text = "✕", Size = new Size(30, 30), FlatStyle = FlatStyle.Flat, ForeColor = ThemeTextMuted, BackColor = ThemeBgBase, Font = FontNormal, Cursor = Cursors.Hand };
            btnClose.FlatAppearance.BorderSize = 0; btnClose.FlatAppearance.MouseOverBackColor = ThemeDanger; btnClose.FlatAppearance.MouseDownBackColor = Color.DarkRed;
            btnClose.Click += delegate { this.Close(); };
            pnlHeader.Resize += delegate { btnClose.Location = new Point(pnlHeader.Width - 30, 0); };

            pnlHeader.Controls.Add(pnlTextVerticalFlow);
            pnlHeader.Controls.Add(btnClose);
            mainLayout.Controls.Add(pnlHeader, 0, 0);

            // ================= 2. 搜索控制栏 =================
            Panel pnlSearchBox = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10), BackColor = ThemeBgInput, Padding = new Padding(1) };
            Panel innerSearch = new Panel { Dock = DockStyle.Fill, BackColor = ThemeBgInput, Padding = new Padding(10, 8, 10, 8) };

            txtSearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = ThemeBgInput, ForeColor = ThemeTextMuted, Font = FontInput, Text = SearchPlaceholder };
            txtSearch.GotFocus += delegate { if (txtSearch.Text == SearchPlaceholder) { txtSearch.Text = ""; txtSearch.ForeColor = Color.White; } };
            txtSearch.LostFocus += delegate { if (string.IsNullOrEmpty(txtSearch.Text) || txtSearch.Text.Trim().Length == 0) { txtSearch.Text = SearchPlaceholder; txtSearch.ForeColor = ThemeTextMuted; } };
            txtSearch.TextChanged += delegate { FilterTagsVisibility(); };

            innerSearch.Controls.Add(txtSearch);
            pnlSearchBox.Controls.Add(innerSearch);
            pnlSearchBox.Paint += DrawPanelBorder;
            mainLayout.Controls.Add(pnlSearchBox, 0, 1);

            // ================= 3. 核心内容区 =================
            TableLayoutPanel contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0) };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            // --- 3.1 自定义命令区 ---
            Panel pnlCustom = new Panel { Dock = DockStyle.Fill, BackColor = ThemeBgSurface, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(15) };
            pnlCustom.Paint += DrawPanelBorder;

            Label lblCustomTitle = new Label { Text = "我的扩展指令 (白名单)", Font = FontNormal, ForeColor = Color.White, Dock = DockStyle.Top, Height = 25 };

            Panel pnlAddContainer = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = ThemeBgInput, Margin = new Padding(0, 0, 0, 10) };
            pnlAddContainer.Paint += DrawPanelBorder;

            txtNewCmd = new TextBox { BorderStyle = BorderStyle.None, BackColor = ThemeBgInput, ForeColor = ThemeTextMuted, Font = FontInput, Text = AddPlaceholder, Location = new Point(10, 9), Width = 350, CharacterCasing = CharacterCasing.Upper };
            txtNewCmd.GotFocus += delegate { if (txtNewCmd.Text.Equals(AddPlaceholder, StringComparison.OrdinalIgnoreCase)) { txtNewCmd.Text = ""; txtNewCmd.ForeColor = ThemeCyan; } };
            txtNewCmd.LostFocus += delegate { if (string.IsNullOrEmpty(txtNewCmd.Text) || txtNewCmd.Text.Trim().Length == 0) { txtNewCmd.Text = AddPlaceholder; txtNewCmd.ForeColor = ThemeTextMuted; } };

            Button btnAdd = new Button { Text = "添加", Dock = DockStyle.Right, Width = 80, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, ThemeCyan.R, ThemeCyan.G, ThemeCyan.B), ForeColor = ThemeCyan, Font = FontNormal, Cursor = Cursors.Hand };
            btnAdd.FlatAppearance.BorderSize = 0; btnAdd.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, ThemeCyan.R, ThemeCyan.G, ThemeCyan.B);

            txtNewCmd.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { btnAdd.PerformClick(); e.SuppressKeyPress = true; } };
            btnAdd.Click += BtnAdd_Click;

            pnlAddContainer.Controls.Add(txtNewCmd); pnlAddContainer.Controls.Add(btnAdd);
            pnlAddContainer.Resize += delegate { txtNewCmd.Width = pnlAddContainer.Width - btnAdd.Width - 20; };

            flpCustom = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeBgSurface };

            pnlCustom.Controls.Add(flpCustom);
            pnlCustom.Controls.Add(pnlAddContainer);
            pnlCustom.Controls.Add(lblCustomTitle);
            contentLayout.Controls.Add(pnlCustom, 0, 0);

            // --- 3.2 核心命令区 ---
            Panel pnlCore = new Panel { Dock = DockStyle.Fill, BackColor = ThemeBgSurface, Padding = new Padding(15) };
            pnlCore.Paint += DrawPanelBorder;

            Panel coreTitleBar = new Panel { Dock = DockStyle.Top, Height = 25 };
            coreTitleBar.Controls.Add(new Label { Text = "系统内置指令", Font = FontNormal, ForeColor = ThemeTextMuted, Dock = DockStyle.Left, AutoSize = true });
            coreTitleBar.Controls.Add(new Label { Text = "⚠ 轻易不要删除核心指令", Font = FontNormal, ForeColor = ThemeDanger, Dock = DockStyle.Right, AutoSize = true });

            flpCore = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeBgSurface, Margin = new Padding(0, 5, 0, 0) };

            pnlCore.Controls.Add(flpCore);
            pnlCore.Controls.Add(coreTitleBar);
            contentLayout.Controls.Add(pnlCore, 0, 1);

            mainLayout.Controls.Add(contentLayout, 0, 2);

            // ================= 4. 底部 Footer =================
            Panel pnlFooter = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };

            Button btnReset = new Button { Text = "恢复默认", Size = new Size(100, 36), FlatStyle = FlatStyle.Flat, ForeColor = ThemeTextMain, BackColor = ThemeBgSurface, Font = FontNormal, Cursor = Cursors.Hand };
            btnReset.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60); btnReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
            btnReset.Click += BtnReset_Click;

            Button btnSave = new Button { Text = "保存并应用", Size = new Size(130, 36), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(20, 20, 20), BackColor = ThemeCyan, Font = CreateSafeFont("微软雅黑", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0; btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 230, 230);
            btnSave.Click += BtnSave_Click;

            // ✅ 修正：使用普通的 Label 替代 LinkLabel，彻底去除下划线，精确调整空格以对齐冒号后侧
            Label lblTips = new Label { Text = "快捷键:双击命令 或 点击命令后\n           按 Delete 删除 | Ctrl+Z 撤销", AutoSize = true, Font = FontNormal, ForeColor = ThemeTextMuted };

            LinkLabel lnkAuthor = new LinkLabel { Text = "© [浅醉·墨语] | 反馈", AutoSize = true, Font = FontNormal, LinkColor = ThemeTextMuted, ActiveLinkColor = Color.White, LinkBehavior = LinkBehavior.HoverUnderline };

            // ✅ 彻底修复链接点击在 CAD 2025/2026 (.NET Core) 环境下的崩溃报错
            lnkAuthor.LinkClicked += delegate {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.douyin.com/user/MS4wLjABAAAA3MenKPeyKOaRDEFrkZW_mBE3e3DNQrCEulaj1adJWFQ")
                    {
                        UseShellExecute = true // ✅ .NET Core 环境下必须显式开启，否则会抛出找不到文件的异常
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法打开链接：" + ex.Message);
                }
            };

            pnlFooter.Controls.Add(lblTips); pnlFooter.Controls.Add(btnReset); pnlFooter.Controls.Add(btnSave); pnlFooter.Controls.Add(lnkAuthor);

            // ✅ 重新排布 Footer 控件，确保不会遮挡
            pnlFooter.Resize += delegate {
                lblTips.Location = new Point(0, 20);
                btnSave.Location = new Point(pnlFooter.Width - btnSave.Width, 10);
                btnReset.Location = new Point(btnSave.Left - btnReset.Width - 10, 10);
                // 将版权信息放置在按钮正下方
                lnkAuthor.Location = new Point(pnlFooter.Width - lnkAuthor.Width, btnSave.Bottom + 12);
            };

            mainLayout.Controls.Add(pnlFooter, 0, 3);

            LoadAndParseData();
            RebuildTags(flpCustom, customCmds, false);
            RebuildTags(flpCore, coreCmds, true);

            SetDarkScrollbar(flpCustom);
            SetDarkScrollbar(flpCore);
        }

        private void DrawPanelBorder(object sender, PaintEventArgs e)
        {
            Panel p = sender as Panel;
            using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60), 1))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, p.Width - 1, p.Height - 1);
            }
        }

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void SetDarkScrollbar(Control ctrl)
        {
            try
            {
                var handle = ctrl.Handle;
                SetWindowTheme(handle, "DarkMode_Explorer", null);
            }
            catch { }
        }
        #endregion

        #region 核心业务逻辑
        private Font CreateSafeFont(string name, float size, FontStyle style)
        {
            try { return new Font(name, size, style); } catch { return new Font(FontFamily.GenericSansSerif, size, style); }
        }

        private void LoadAndParseData()
        {
            List<string> allDiskCmds = ConfigManager.ReadAllCommandsFromDisk();
            coreCmds.Clear(); customCmds.Clear(); customCmdSet.Clear(); coreCmdSet.Clear();

            foreach (string cmd in ConfigManager.DefaultCommands) coreCmdSet[cmd] = true;
            foreach (string cmd in allDiskCmds)
            {
                if (coreCmdSet.ContainsKey(cmd)) coreCmds.Add(cmd);
                else { customCmds.Add(cmd); customCmdSet[cmd] = true; }
            }
        }

        private void RebuildTags(FlowLayoutPanel flp, List<string> cmds, bool isCore)
        {
            flp.SuspendLayout();
            flp.Controls.Clear();
            if (isCore) selectedCoreTag = null; else selectedCustomTag = null;

            foreach (string cmd in cmds)
            {
                Label chip = new Label { Text = cmd, AutoSize = true, Font = FontChip, BackColor = ThemeBgInput, ForeColor = isCore ? ThemeTextMuted : Color.White, Padding = new Padding(12, 6, 12, 6), Margin = new Padding(4), Cursor = Cursors.Hand, Tag = cmd };

                chip.MouseEnter += delegate { if (chip != selectedCustomTag && chip != selectedCoreTag) chip.BackColor = Color.FromArgb(70, 70, 75); };
                chip.MouseLeave += delegate { StyleChip(chip, chip == selectedCustomTag || chip == selectedCoreTag, isCore); };
                chip.Click += Chip_Click;
                chip.DoubleClick += Chip_DoubleClick;
                flp.Controls.Add(chip);
            }
            flp.ResumeLayout();
            FilterTagsVisibility();
        }

        private void FilterTagsVisibility()
        {
            string filter = txtSearch.Text.Trim().ToUpper();
            if (filter == SearchPlaceholder.ToUpper()) filter = "";

            flpCustom.SuspendLayout(); flpCore.SuspendLayout();
            foreach (Control ctrl in flpCustom.Controls) if (ctrl.Tag != null) ctrl.Visible = string.IsNullOrEmpty(filter) || ctrl.Tag.ToString().Contains(filter);
            foreach (Control ctrl in flpCore.Controls) if (ctrl.Tag != null) ctrl.Visible = string.IsNullOrEmpty(filter) || ctrl.Tag.ToString().Contains(filter);
            flpCustom.ResumeLayout(); flpCore.ResumeLayout();
        }

        private void Chip_Click(object sender, EventArgs e)
        {
            Label chip = sender as Label;
            this.ActiveControl = null;
            bool isCore = flpCore.Controls.Contains(chip);

            if (isCore)
            {
                if (selectedCoreTag != null) StyleChip(selectedCoreTag, false, true);
                selectedCoreTag = chip; StyleChip(selectedCoreTag, true, true);
            }
            else
            {
                if (selectedCustomTag != null) StyleChip(selectedCustomTag, false, false);
                selectedCustomTag = chip; StyleChip(selectedCustomTag, true, false);
            }
        }

        private void StyleChip(Label lbl, bool isSelected, bool isCore)
        {
            lbl.BackColor = isSelected ? ThemeCyan : ThemeBgInput;
            lbl.ForeColor = isSelected ? Color.FromArgb(20, 20, 20) : (isCore ? ThemeTextMuted : Color.White);
        }

        private void Chip_DoubleClick(object sender, EventArgs e)
        {
            Label chip = sender as Label; string cmd = chip.Tag.ToString(); bool isCore = flpCore.Controls.Contains(chip);
            if (isCore && MessageBox.Show("警告：移除核心命令可能导致系统异常。继续？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ExecuteDelete(cmd, isCore);
        }

        private void ExecuteDelete(string cmd, bool isCore)
        {
            List<string> cmdList = isCore ? coreCmds : customCmds;
            int idx = cmdList.IndexOf(cmd);
            undoStack.Push(new UndoAction { Command = cmd, IsCore = isCore, OriginalIndex = idx }); redoStack.Clear();
            cmdList.Remove(cmd);
            if (!isCore) customCmdSet.Remove(cmd);
            RebuildTags(isCore ? flpCore : flpCustom, cmdList, isCore);
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string input = txtNewCmd.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(input) || input.Equals(AddPlaceholder.ToUpper(), StringComparison.OrdinalIgnoreCase)) return;

            string[] multiCmds = input.Split(new char[] { ' ', ',', ';', '；', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasAdded = false;

            foreach (string cmdRaw in multiCmds)
            {
                string cmd = cmdRaw.Trim('\uFEFF', '\u200B').Trim();
                if (string.IsNullOrEmpty(cmd)) continue;
                if (coreCmdSet.ContainsKey(cmd) || customCmdSet.ContainsKey(cmd)) continue;

                customCmds.Insert(0, cmd);
                customCmdSet[cmd] = true;
                hasAdded = true;
            }

            if (hasAdded)
            {
                txtNewCmd.Clear(); txtNewCmd.Focus();
                RebuildTags(flpCustom, customCmds, false);
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("是否恢复系统内置指令表？\n(您的自定义扩展指令不会受影响)", "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                coreCmds = new List<string>(ConfigManager.DefaultCommands);
                undoStack.Clear(); redoStack.Clear();
                RebuildTags(flpCore, coreCmds, true);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            List<string> finalCmds = new List<string>(coreCmds);
            finalCmds.AddRange(customCmds);
            ConfigManager.SaveAllCommands(finalCmds);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.Z)
            {
                if (txtNewCmd.Focused || txtSearch.Focused) return;
                if (undoStack.Count > 0)
                {
                    UndoAction last = undoStack.Pop(); redoStack.Push(last);
                    List<string> cmdList = last.IsCore ? coreCmds : customCmds;
                    if (!cmdList.Contains(last.Command))
                    {
                        if (last.OriginalIndex >= 0 && last.OriginalIndex < cmdList.Count) cmdList.Insert(last.OriginalIndex, last.Command);
                        else cmdList.Add(last.Command);
                        if (!last.IsCore) customCmdSet[last.Command] = true;
                    }
                    RebuildTags(last.IsCore ? flpCore : flpCustom, cmdList, last.IsCore);
                }
            }
            else if ((ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.Y)
            {
                if (txtNewCmd.Focused || txtSearch.Focused) return;
                if (redoStack.Count > 0)
                {
                    UndoAction last = redoStack.Pop(); undoStack.Push(last);
                    List<string> cmdList = last.IsCore ? coreCmds : customCmds;
                    cmdList.Remove(last.Command);
                    if (!last.IsCore) customCmdSet.Remove(last.Command);
                    RebuildTags(last.IsCore ? flpCore : flpCustom, cmdList, last.IsCore);
                }
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (txtNewCmd.Focused || txtSearch.Focused) return;
                if (selectedCustomTag != null) ExecuteDelete(selectedCustomTag.Tag.ToString(), false);
                else if (selectedCoreTag != null && MessageBox.Show("确定移除核心指令 [" + selectedCoreTag.Tag + "] 吗？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ExecuteDelete(selectedCoreTag.Tag.ToString(), true);
            }
            else if (e.KeyCode == Keys.Escape) { this.Close(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { FontTitle.Dispose(); FontSubtitle.Dispose(); FontInput.Dispose(); FontChip.Dispose(); FontNormal.Dispose(); }
            base.Dispose(disposing);
        }
        #endregion
    }
}