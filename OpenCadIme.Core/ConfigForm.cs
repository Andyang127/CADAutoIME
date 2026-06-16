using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenCadIme
{
    public class ConfigForm : Form
    {
        #region 颜色静态资源（统一复用，避免GDI泄漏）
        private static readonly Color cadBgDark = Color.FromArgb(34, 38, 42);
        private static readonly Color cadBgDarker = Color.FromArgb(24, 27, 30);
        private static readonly Color cadBgPanel = Color.FromArgb(45, 52, 54);
        private static readonly Color cadCyan = Color.FromArgb(0, 210, 211);
        private static readonly Color cadTextPrimary = Color.FromArgb(235, 235, 235);
        private static readonly Color cadTextMuted = Color.FromArgb(150, 150, 150);
        private static readonly Color cadBorder = Color.FromArgb(60, 65, 70);
        private static readonly Font FontTitleMain = new Font("微软雅黑", 16, FontStyle.Bold);
        private static readonly Font FontVersion = new Font("Consolas", 11, FontStyle.Bold | FontStyle.Italic);
        private static readonly Font FontChip = new Font("Consolas", 9.5F);
        private static readonly Font FontNormal = new Font("微软雅黑", 9F);
        #endregion

        #region 窗体字段
        private List<string> customCmds = new List<string>();
        private List<string> coreCmds = new List<string>();
        private class UndoAction
        {
            public string Command { get; set; }
            public bool IsCore { get; set; }
            public int OriginalIndex { get; set; }
        }
        private Stack<UndoAction> undoStack = new Stack<UndoAction>();
        private Stack<UndoAction> redoStack = new Stack<UndoAction>();
        private FlowLayoutPanel flpCustom;
        private FlowLayoutPanel flpCore;
        private TextBox txtNewCmd;
        private TextBox txtSearch;
        private Label selectedCustomTag = null;
        private Label selectedCoreTag = null;
        private Panel pnlHeader;
        private Point mouseOffset; // 窗口拖拽偏移
        private const int FormRoundRadius = 8; // 窗体圆角大小
        private const int ButtonRoundRadius = 6; // 按钮圆角
        #endregion

        public ConfigForm()
        {
            #region 基础窗口设置 - 无边框移除外层蓝色框
            this.Text = "输入法白名单配置 - CAD Auto IME";
            this.ClientSize = new Size(560, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None; // 去除系统蓝色外边框
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = cadBgDark;
            this.ForeColor = cadTextPrimary;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.KeyPreview = true;
            this.KeyDown += Form_KeyDown;
            this.Paint += Form_Paint; // 绘制全局窗体圆角
            #endregion

            #region DPI缩放计算
            float dpiScale = 1f;
            using (Graphics g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96f;
            }
            this.ClientSize = new Size((int)(560 * dpiScale), (int)(480 * dpiScale));
            #endregion

            #region 主布局容器
            TableLayoutPanel mainGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Margin = new Padding(10) // 圆角边距留白
            };
            mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 85F * dpiScale));
            mainGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F * dpiScale));
            this.Controls.Add(mainGrid);
            #endregion

            #region 1. 顶部自定义标题栏（可拖拽+右上角关闭按钮）
            pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = cadBgPanel };
            // 拖拽逻辑绑定
            pnlHeader.MouseDown += Header_MouseDown;
            pnlHeader.MouseMove += Header_MouseMove;
            pnlHeader.MouseUp += Header_MouseUp;

            // 标题文字
            FlowLayoutPanel titleFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                Location = new Point(20, 15),
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            Label lblTitle = new Label
            {
                Text = "CAD输入法自动切换助手",
                AutoSize = true,
                Font = FontTitleMain,
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 8, 0),
                Anchor = AnchorStyles.Bottom
            };
            Label lblVersion = new Label
            {
                Text = "v0.2",
                AutoSize = true,
                Font = FontVersion,
                ForeColor = cadCyan,
                Margin = new Padding(0, 0, 0, 4),
                Anchor = AnchorStyles.Bottom
            };
            titleFlow.Controls.Add(lblTitle);
            titleFlow.Controls.Add(lblVersion);
            pnlHeader.Controls.Add(titleFlow);

            // 副标题固定不变
            Label lblSlogan = new Label
            {
                Text = "让每一位设计师都能专注于创作，而非输入法切换",
                AutoSize = true,
                Font = FontNormal,
                ForeColor = Color.FromArgb(170, 170, 170),
                Location = new Point(24, 50)
            };
            pnlHeader.Controls.Add(lblSlogan);

            // 右上角关闭按钮
            Button btnClose = new Button
            {
                Text = "×",
                Size = new Size(32, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = cadTextPrimary,
                Font = new Font("微软雅黑", 12, FontStyle.Bold),
                Location = new Point(pnlHeader.Width - 42, 8),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 60, 60);
            btnClose.Click += (s, e) => this.Close();
            pnlHeader.Controls.Add(btnClose);

            // 标题栏底部分割线
            pnlHeader.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(cadBorder, 1.5f), 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };
            mainGrid.Controls.Add(pnlHeader, 0, 0);
            #endregion

            #region 2. 主体内容区（搜索+自定义命令+核心命令）
            TableLayoutPanel bodyGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = new Padding(0, 10, 0, 10)
            };
            bodyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bodyGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            bodyGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            mainGrid.Controls.Add(bodyGrid, 0, 1);

            // ========== 自定义扩展命令区域 ==========
            Panel pnlCustom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 15, 20, 5) };
            Panel customTitleBar = new Panel { Dock = DockStyle.Top, Height = (int)(25 * dpiScale) };
            Label lblCustomTitle = new Label
            {
                Text = "▶ 我的扩展命令",
                Font = new Font("微软雅黑", 9.5F, FontStyle.Bold),
                ForeColor = cadTextPrimary,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            Label lblTip = new Label
            {
                Text = "* 双击/Delete删除 | Ctrl+Z撤销 | Ctrl+Y重做",
                Font = new Font("微软雅黑", 8.5f),
                ForeColor = cadCyan,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true
            };
            customTitleBar.Controls.Add(lblTip);
            customTitleBar.Controls.Add(lblCustomTitle);

            // 搜索框
            Panel searchBar = new Panel { Dock = DockStyle.Top, Height = (int)(32 * dpiScale), Margin = new Padding(0, 4, 0, 4) };
            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = cadBgDarker,
                ForeColor = cadCyan,
                BorderStyle = BorderStyle.FixedSingle,
                Font = FontNormal
            };
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == "🔍 搜索命令...") txtSearch.Text = ""; txtSearch.ForeColor = cadTextPrimary; };
            txtSearch.LostFocus += (s, e) => { if (string.IsNullOrEmpty(txtSearch.Text) || txtSearch.Text.Trim().Length == 0) { txtSearch.Text = "🔍 搜索命令..."; txtSearch.ForeColor = cadTextMuted; } };
            txtSearch.Text = "🔍 搜索命令...";
            txtSearch.ForeColor = cadTextMuted;
            txtSearch.TextChanged += (s, e) => RefreshTags(flpCustom, customCmds, false);
            searchBar.Controls.Add(txtSearch);

            // 添加命令输入栏
            Panel customInputBar = new Panel { Dock = DockStyle.Top, Height = (int)(38 * dpiScale), Padding = new Padding(0, 4, 0, 4) };
            txtNewCmd = new TextBox
            {
                Dock = DockStyle.Fill,
                CharacterCasing = CharacterCasing.Upper,
                Font = new Font("Consolas", 12F),
                BackColor = cadBgDarker,
                ForeColor = cadCyan,
                BorderStyle = BorderStyle.FixedSingle
            };
            Button btnAdd = new Button
            {
                Text = "＋ 添 加",
                Dock = DockStyle.Right,
                Width = (int)(80 * dpiScale),
                FlatStyle = FlatStyle.Flat,
                BackColor = cadBgPanel,
                ForeColor = cadTextPrimary,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderColor = cadBorder;
            btnAdd.FlatAppearance.MouseOverBackColor = cadBorder;
            customInputBar.Controls.Add(txtNewCmd);
            customInputBar.Controls.Add(btnAdd);
            txtNewCmd.BringToFront();
            txtNewCmd.Enter += ClearSelectedAll;
            txtNewCmd.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { btnAdd.PerformClick(); e.SuppressKeyPress = true; } };
            btnAdd.Click += BtnAdd_Click;

            // 修复截断核心：修正滚动容器偏移，不再挤压标签
            flpCustom = new FlowLayoutPanel
            {
                BackColor = cadBgDarker,
                Padding = new Padding(4),
                AutoScroll = true,
                WrapContents = true
            };
            Panel customBorderPanel = CreateScrollWrapper(flpCustom, dpiScale);

            pnlCustom.Controls.Add(customBorderPanel);
            pnlCustom.Controls.Add(customInputBar);
            pnlCustom.Controls.Add(searchBar);
            pnlCustom.Controls.Add(customTitleBar);
            bodyGrid.Controls.Add(pnlCustom, 0, 0);

            // ========== 系统核心命令区域 ==========
            Panel pnlCore = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 5, 20, 15) };
            Panel coreTitleBar = new Panel { Dock = DockStyle.Top, Height = (int)(25 * dpiScale) };
            Label lblCoreTitle = new Label
            {
                Text = "▶ 系统核心规则 (Auto CAD 规则内置)",
                Font = new Font("微软雅黑", 9.5F, FontStyle.Bold),
                ForeColor = cadTextMuted,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            coreTitleBar.Controls.Add(lblCoreTitle);

            flpCore = new FlowLayoutPanel
            {
                BackColor = cadBgDarker,
                Padding = new Padding(4),
                AutoScroll = true,
                WrapContents = true
            };
            Panel coreBorderPanel = CreateScrollWrapper(flpCore, dpiScale);
            pnlCore.Controls.Add(coreBorderPanel);
            pnlCore.Controls.Add(coreTitleBar);
            bodyGrid.Controls.Add(pnlCore, 0, 1);
            #endregion

            #region 3. 底部操作栏（圆角按钮绘制）
            Panel footerPanel = new Panel { Dock = DockStyle.Fill, BackColor = cadBgPanel };
            footerPanel.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(cadBorder, 1.5f), 0, 0, footerPanel.Width, 0); };

            Panel actionBox = new Panel
            {
                Size = new Size((int)(280 * dpiScale), (int)(44 * dpiScale)),
                BackColor = Color.FromArgb(228, 245, 235)
            };
            actionBox.Paint += (s, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, actionBox.Width - 1, actionBox.Height - 1);
                using (GraphicsPath path = GetRoundRectPath(rect, ButtonRoundRadius))
                using (Pen pen = new Pen(Color.FromArgb(170, 215, 185), 1))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            // 恢复默认按钮（圆角绘制）
            Button btnReset = new Button
            {
                Text = "恢复默认",
                FlatStyle = FlatStyle.Flat,
                Font = FontNormal,
                ForeColor = Color.FromArgb(80, 80, 80),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Size = new Size((int)(100 * dpiScale), (int)(32 * dpiScale))
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(205, 235, 215);
            btnReset.Paint += DrawRoundButton;
            // 保存按钮（圆角绘制）
            Button btnSave = new Button
            {
                Text = "保存并生效",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(20, 160, 110),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Size = new Size((int)(130 * dpiScale), (int)(32 * dpiScale))
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 180, 125);
            btnSave.Paint += DrawRoundButton;

            int gap = 12;
            int totalW = btnReset.Width + gap + btnSave.Width;
            int btnStartX = (actionBox.Width - totalW) / 2;
            btnReset.Location = new Point(btnStartX, (actionBox.Height - btnReset.Height) / 2);
            btnSave.Location = new Point(btnReset.Right + gap, (actionBox.Height - btnSave.Height) / 2);
            actionBox.Controls.Add(btnReset);
            actionBox.Controls.Add(btnSave);

            // 底部版权链接
            LinkLabel lnkAuthor = new LinkLabel
            {
                Text = "© [浅醉·墨语] | 反馈",
                AutoSize = true,
                Font = FontNormal,
                LinkColor = cadTextMuted,
                ActiveLinkColor = Color.White,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            lnkAuthor.LinkClicked += (s, e) =>
            {
                System.Diagnostics.Process.Start("https://www.douyin.com/user/MS4wLjABAAAA3MenKPeyKOaRDEFrkZW_mBE3e3DNQrCEulaj1adJWFQ");
            };
            footerPanel.Controls.Add(actionBox);
            footerPanel.Controls.Add(lnkAuthor);
            footerPanel.Resize += (s, e) =>
            {
                actionBox.Location = new Point((footerPanel.Width - actionBox.Width) / 2, 16);
                lnkAuthor.Location = new Point(footerPanel.Width - lnkAuthor.Width - 20, footerPanel.Height - lnkAuthor.Height - 12);
            };
            btnReset.Click += BtnReset_Click;
            btnSave.Click += BtnSave_Click;
            mainGrid.Controls.Add(footerPanel, 0, 2);
            #endregion

            // 加载数据+渲染标签
            LoadAndParseData();
            RefreshTags(flpCustom, customCmds, false);
            RefreshTags(flpCore, coreCmds, true);
            this.Shown += (s, e) => txtNewCmd.Focus();
        }

        #region 窗口圆角绘制
        private void Form_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = new Rectangle(0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
            using (GraphicsPath path = GetRoundRectPath(rect, FormRoundRadius))
            {
                this.Region = new Region(path);
            }
        }
        /// <summary>生成圆角矩形路径</summary>
        private GraphicsPath GetRoundRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseAllFigures();
            return path;
        }
        /// <summary>按钮圆角绘制</summary>
        private void DrawRoundButton(object sender, PaintEventArgs e)
        {
            Button btn = sender as Button;
            Rectangle rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
            using (GraphicsPath path = GetRoundRectPath(rect, ButtonRoundRadius))
            {
                e.Graphics.SetClip(path);
            }
        }
        #endregion

        #region 标题栏拖拽逻辑
        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            mouseOffset = new Point(-e.X, -e.Y);
        }
        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mousePos = Control.MousePosition;
                mousePos.Offset(mouseOffset.X, mouseOffset.Y);
                this.Location = mousePos;
            }
        }
        private void Header_MouseUp(object sender, MouseEventArgs e) { }
        #endregion

        #region 滚动容器修复（解决标签文字截断）
        private Panel CreateScrollWrapper(FlowLayoutPanel flp, float dpiScale)
        {
            Panel wrapper = new Panel { Dock = DockStyle.Fill, BackColor = cadBorder, Padding = new Padding(1), Margin = new Padding(0, 5, 0, 0) };
            Panel innerClip = new Panel { Dock = DockStyle.Fill, BackColor = cadBgDarker };
            flp.Dock = DockStyle.None;
            flp.Padding = new Padding(4, 4, 4, 35); // 移除超大滚动偏移，修复文字截断
            innerClip.Controls.Add(flp);
            wrapper.Controls.Add(innerClip);
            innerClip.Resize += (s, e) =>
            {
                flp.SetBounds(0, 0, innerClip.Width, innerClip.Height);
            };
            // 自定义简易滚动条绘制
            innerClip.Paint += (s, e) =>
            {
                if (!flp.VerticalScroll.Visible) return;
                int max = flp.VerticalScroll.Maximum;
                int view = innerClip.Height;
                if (max <= view) return;
                int thumbHeight = Math.Max(25, (int)((float)view / max * view));
                float percent = (float)flp.VerticalScroll.Value / (max - view + 1);
                int thumbY = (int)(percent * (view - thumbHeight));
                using (SolidBrush br = new SolidBrush(Color.FromArgb(140, cadCyan)))
                {
                    e.Graphics.FillRectangle(br, innerClip.Width - 6, thumbY, 6, thumbHeight);
                }
            };
            flp.Scroll += (s, e) => innerClip.Invalidate();
            flp.MouseWheel += (s, e) => innerClip.Invalidate();
            return wrapper;
        }
        #endregion

        #region 数据加载、标签渲染、搜索过滤（修复文字截断）
        private void LoadAndParseData()
        {
            List<string> allDiskCmds = ConfigManager.ReadAllCommandsFromDisk();
            HashSet<string> coreSet = new HashSet<string>(ConfigManager.DefaultCommands, StringComparer.OrdinalIgnoreCase);
            coreCmds.Clear();
            customCmds.Clear();
            foreach (string cmd in allDiskCmds)
            {
                if (coreSet.Contains(cmd)) coreCmds.Add(cmd);
                else customCmds.Add(cmd);
            }
        }

        private void RefreshTags(FlowLayoutPanel flp, List<string> cmds, bool isCore)
        {
            flp.SuspendLayout();
            flp.Controls.Clear();
            if (isCore) selectedCoreTag = null;
            else selectedCustomTag = null;

            // 搜索过滤
            string filter = txtSearch?.Text.Trim().ToUpper() ?? "";
            if (filter == "🔍 搜索命令...") filter = "";

            foreach (string cmd in cmds)
            {
                if (!string.IsNullOrEmpty(filter) && !cmd.Contains(filter)) continue;
                // 圆角命令标签
                Label chip = new Label
                {
                    Text = cmd,
                    AutoSize = true,
                    Font = FontChip,
                    BackColor = cadBgPanel,
                    ForeColor = isCore ? cadTextMuted : cadTextPrimary,
                    Padding = new Padding(6, 3, 6, 3),
                    Margin = new Padding(3),
                    Cursor = Cursors.Hand,
                    Tag = cmd
                };
                chip.Paint += DrawChipRound;
                chip.MouseEnter += Chip_HoverEnter;
                chip.MouseLeave += Chip_HoverLeave;
                chip.Click += Chip_Click;
                chip.DoubleClick += Chip_DoubleClick;
                flp.Controls.Add(chip);
            }
            // 底部占位空label，避免最后一行紧贴边界
            Label spacer = new Label { Width = 0, Height = 30, BackColor = Color.Transparent, Margin = new Padding(0) };
            flp.Controls.Add(spacer);
            flp.ResumeLayout();
            flp.Parent?.Invalidate();
        }

        // 标签圆角绘制
        private void DrawChipRound(object sender, PaintEventArgs e)
        {
            Label lbl = sender as Label;
            Rectangle rect = new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1);
            using (GraphicsPath path = GetRoundRectPath(rect, 4))
            {
                e.Graphics.SetClip(path);
            }
        }
        // 标签hover高亮
        private void Chip_HoverEnter(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            lbl.BackColor = cadCyan;
            lbl.ForeColor = Color.FromArgb(20, 20, 20);
            lbl.Invalidate();
        }
        private void Chip_HoverLeave(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool isCore = flpCore.Controls.Contains(lbl);
            StyleChip(lbl, lbl == selectedCustomTag || lbl == selectedCoreTag, isCore);
        }
        // 标签点击选中
        private void Chip_Click(object sender, EventArgs e)
        {
            Label chip = sender as Label;
            this.ActiveControl = null;
            bool isCore = flpCore.Controls.Contains(chip);
            if (isCore)
            {
                if (selectedCoreTag != null) StyleChip(selectedCoreTag, false, true);
                selectedCoreTag = chip;
                StyleChip(selectedCoreTag, true, true);
            }
            else
            {
                if (selectedCustomTag != null) StyleChip(selectedCustomTag, false, false);
                selectedCustomTag = chip;
                StyleChip(selectedCustomTag, true, false);
            }
        }
        // 双击删除标签
        private void Chip_DoubleClick(object sender, EventArgs e)
        {
            Label chip = sender as Label;
            string cmd = chip.Tag.ToString();
            bool isCore = flpCore.Controls.Contains(chip);
            this.ActiveControl = null;
            if (isCore)
            {
                if (MessageBox.Show($"警告：移除核心命令 [{cmd}] 可能导致系统异常。是否继续？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                int idx = coreCmds.IndexOf(cmd);
                undoStack.Push(new UndoAction { Command = cmd, IsCore = true, OriginalIndex = idx });
                redoStack.Clear();
                coreCmds.Remove(cmd);
                RefreshTags(flpCore, coreCmds, true);
            }
            else
            {
                int idx = customCmds.IndexOf(cmd);
                undoStack.Push(new UndoAction { Command = cmd, IsCore = false, OriginalIndex = idx });
                redoStack.Clear();
                customCmds.Remove(cmd);
                RefreshTags(flpCustom, customCmds, false);
            }
        }
        // 选中/取消选中样式
        private void StyleChip(Label lbl, bool isSelected, bool isCore)
        {
            if (isSelected)
            {
                lbl.BackColor = cadCyan;
                lbl.ForeColor = Color.FromArgb(20, 20, 20);
            }
            else
            {
                lbl.BackColor = cadBgPanel;
                lbl.ForeColor = isCore ? cadTextMuted : cadTextPrimary;
            }
            lbl.Invalidate();
        }
        // 清空所有标签选中状态
        private void ClearSelectedAll(object sender, EventArgs e)
        {
            if (selectedCustomTag != null) { StyleChip(selectedCustomTag, false, false); selectedCustomTag = null; }
            if (selectedCoreTag != null) { StyleChip(selectedCoreTag, false, true); selectedCoreTag = null; }
        }
        #endregion

        #region 按钮点击事件
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string inputRaw = txtNewCmd.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(inputRaw)) return;
            // 支持多分隔符批量添加
            char[] splitChars = new char[] { ' ', ',', '；', ';' };
            string[] multiCmds = inputRaw.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> coreSet = new HashSet<string>(ConfigManager.DefaultCommands, StringComparer.OrdinalIgnoreCase);
            foreach (string cmd in multiCmds)
            {
                if (coreSet.Contains(cmd) || customCmds.Contains(cmd))
                {
                    MessageBox.Show($"指令 [{cmd}] 已存在！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    continue;
                }
                customCmds.Insert(0, cmd);
            }
            txtNewCmd.Clear();
            txtNewCmd.Focus();
            RefreshTags(flpCustom, customCmds, false);
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("是否恢复系统内置指令表？\n(您的自定义扩展指令不会受到影响)", "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            coreCmds = new List<string>(ConfigManager.DefaultCommands);
            undoStack.Clear();
            redoStack.Clear();
            RefreshTags(flpCore, coreCmds, true);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            List<string> finalCmds = new List<string>();
            finalCmds.AddRange(coreCmds);
            finalCmds.AddRange(customCmds);
            ConfigManager.SaveAllCommands(finalCmds);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        #endregion

        #region 全局快捷键 Ctrl+Z / Ctrl+Y / Delete / Esc / Ctrl+A
        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Z 撤销
            if ((ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.Z)
            {
                if (txtNewCmd.Focused || txtSearch.Focused) return;
                if (undoStack.Count > 0)
                {
                    UndoAction last = undoStack.Pop();
                    redoStack.Push(last);
                    if (last.IsCore)
                    {
                        if (!coreCmds.Contains(last.Command))
                        {
                            if (last.OriginalIndex >= 0 && last.OriginalIndex <= coreCmds.Count)
                                coreCmds.Insert(last.OriginalIndex, last.Command);
                            else coreCmds.Add(last.Command);
                            RefreshTags(flpCore, coreCmds, true);
                        }
                    }
                    else
                    {
                        if (!customCmds.Contains(last.Command))
                        {
                            if (last.OriginalIndex >= 0 && last.OriginalIndex <= customCmds.Count)
                                customCmds.Insert(last.OriginalIndex, last.Command);
                            else customCmds.Add(last.Command);
                            RefreshTags(flpCustom, customCmds, false);
                        }
                    }
                    e.SuppressKeyPress = true;
                }
                return;
            }
            // Ctrl+Y 重做
            if ((ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.Y)
            {
                if (txtNewCmd.Focused || txtSearch.Focused) return;
                if (redoStack.Count > 0)
                {
                    UndoAction last = redoStack.Pop();
                    undoStack.Push(last);
                    if (last.IsCore) coreCmds.Remove(last.Command);
                    else customCmds.Remove(last.Command);
                    RefreshTags(flpCustom, customCmds, false);
                    RefreshTags(flpCore, coreCmds, true);
                    e.SuppressKeyPress = true;
                }
                return;
            }
            // Delete 删除选中标签
            if (e.KeyCode == Keys.Delete)
            {
                if (txtNewCmd.Focused || txtSearch.Focused) return;
                bool changed = false;
                if (selectedCustomTag != null)
                {
                    string cmd = selectedCustomTag.Tag.ToString();
                    int idx = customCmds.IndexOf(cmd);
                    undoStack.Push(new UndoAction { Command = cmd, IsCore = false, OriginalIndex = idx });
                    redoStack.Clear();
                    customCmds.Remove(cmd);
                    RefreshTags(flpCustom, customCmds, false);
                    changed = true;
                }
                else if (selectedCoreTag != null)
                {
                    string cmd = selectedCoreTag.Tag.ToString();
                    if (MessageBox.Show($"确定移除核心指令 [{cmd}] 吗？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        int idx = coreCmds.IndexOf(cmd);
                        undoStack.Push(new UndoAction { Command = cmd, IsCore = true, OriginalIndex = idx });
                        redoStack.Clear();
                        coreCmds.Remove(cmd);
                        RefreshTags(flpCore, coreCmds, true);
                        changed = true;
                    }
                }
                if (changed) e.SuppressKeyPress = true;
                return;
            }
            // Esc 清空选中
            if (e.KeyCode == Keys.Escape)
            {
                ClearSelectedAll(null, null);
                e.SuppressKeyPress = true;
                return;
            }
            // Ctrl+A 全选自定义区域过滤
            if ((ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.A)
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
                e.SuppressKeyPress = true;
                return;
            }
        }
        #endregion
    }
}