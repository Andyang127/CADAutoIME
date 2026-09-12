using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenCadIme.Core;
using OpenCadIme.UI;

namespace OpenCadIme.UI.ModernWpf
{
    public partial class ConfigWindow : Window
    {
        private List<string> customCmds = new List<string>();
        private List<string> coreCmds = new List<string>();
        private Dictionary<string, bool> customCmdSet = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, bool> coreCmdSet = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private class UndoAction { public string Command { get; set; } public bool IsCore { get; set; } public int OriginalIndex { get; set; } }
        private Stack<UndoAction> undoStack = new Stack<UndoAction>();
        private Stack<UndoAction> redoStack = new Stack<UndoAction>();

        private Border selectedCustomTag = null;
        private Border selectedCoreTag = null;

        private System.Windows.Threading.DispatcherTimer statusTimer;

        public ConfigWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            lblTitle.Text = $"{AppConstants.PluginShortName} 白名单配置";
            lblVersion.Text = AppConstants.VersionDisplay;

            LoadIconDynamically();

            statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            statusTimer.Tick += (s, e) => { lblStatus.Visibility = Visibility.Collapsed; statusTimer.Stop(); };

            this.Loaded += ConfigWindow_Loaded;
            this.PreviewKeyDown += Window_PreviewKeyDown;
        }

        private bool _isDarkTheme = true;

        private void ApplyAutoCadSmartTheme()
        {
            _isDarkTheme = true;
            try
            {
                if (Autodesk.AutoCAD.ApplicationServices.Application.Version.Major < 20) _isDarkTheme = false;
                else
                {
                    object themeVar = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("COLORTHEME");
                    if (themeVar != null && Convert.ToInt16(themeVar) == 1) _isDarkTheme = false;
                }
            }
            catch { }

            if (!_isDarkTheme)
            {
                this.Resources["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
                this.Resources["CardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["TitleBarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8"));
                this.Resources["TextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"));
                this.Resources["TextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606060"));
                this.Resources["BorderLine"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D5D5D5"));
                this.Resources["ButtonBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEEEE"));
                this.Resources["ButtonHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
                this.Resources["AccentColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#006CBE"));
                this.Resources["AccentHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));
                this.Resources["InputBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                this.Resources["ThumbNormalBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B8C4"));
                this.Resources["ThumbHoverBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#828F9F"));

                if (txtSearchWatermark != null)
                    txtSearchWatermark.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8C95A0"));
                if (lblVersion != null)
                    lblVersion.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#006CBE"));
            }
        }

        private void ConfigWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyAutoCadSmartTheme();
            LoadAndParseData();
            RbGlobal.IsChecked = UiConfigManager.IsGlobalMode;
            RbProcess.IsChecked = !UiConfigManager.IsGlobalMode;

            RebuildTags(wpCustom, customCmds, false);
            RebuildTags(wpCore, coreCmds, true);

            UpdateManager.CheckForUpdates(latestVersion =>
            {
                this.Dispatcher.InvokeAsync(() =>
                {
                    if (!this.IsLoaded || !this.IsVisible) return;

                    lblCheckUpdate.Visibility = Visibility.Visible;
                    lblCheckUpdate.Text = $"[发现新版本: {latestVersion}]";
                    lblCheckUpdate.Tag = latestVersion;
                });
            });
        }

        private void LoadAndParseData()
        {
            List<string> allDiskCmds = UiConfigManager.ReadAllCommandsFromDisk();
            coreCmds.Clear(); customCmds.Clear(); customCmdSet.Clear(); coreCmdSet.Clear();
            foreach (string cmd in UiConfigManager.DefaultCommands) coreCmdSet[cmd] = true;
            foreach (string cmd in allDiskCmds)
            {
                if (coreCmdSet.ContainsKey(cmd)) coreCmds.Add(cmd);
                else { customCmds.Add(cmd); customCmdSet[cmd] = true; }
            }
        }

        private void RebuildTags(WrapPanel wp, List<string> cmds, bool isCore)
        {
            wp.Children.Clear();
            if (isCore) selectedCoreTag = null; else selectedCustomTag = null;

            Color defaultBg = _isDarkTheme ? Color.FromRgb(55, 55, 58) : Color.FromRgb(240, 243, 246);
            Color defaultBorder = _isDarkTheme ? Color.FromRgb(85, 85, 85) : Color.FromRgb(215, 220, 226);
            Color defaultText = _isDarkTheme
                ? (isCore ? Color.FromRgb(180, 180, 180) : Color.FromRgb(245, 245, 245))
                : (isCore ? Color.FromRgb(100, 105, 115) : Color.FromRgb(30, 35, 45));

            foreach (string cmd in cmds)
            {
                Border chip = new Border
                {
                    Background = new SolidColorBrush(defaultBg),
                    BorderBrush = new SolidColorBrush(defaultBorder),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand,
                    Tag = cmd
                };

                TextBlock txt = new TextBlock
                {
                    Text = cmd,
                    Foreground = new SolidColorBrush(defaultText),
                    FontSize = 12.5,
                    FontFamily = new FontFamily("Consolas, Segoe UI")
                };
                chip.Child = txt;

                chip.MouseEnter += (s, ev) =>
                {
                    if (chip != selectedCustomTag && chip != selectedCoreTag)
                    {
                        chip.Background = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(75, 75, 80) : Color.FromRgb(225, 230, 238));
                        chip.BorderBrush = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(120, 120, 120) : Color.FromRgb(0, 108, 190));
                    }
                };
                chip.MouseLeave += (s, ev) => { StyleChip(chip, chip == selectedCustomTag || chip == selectedCoreTag, isCore); };
                chip.MouseLeftButtonDown += Chip_MouseLeftButtonDown;

                wp.Children.Add(chip);
            }
            FilterTagsVisibility();
        }

        private void StyleChip(Border lbl, bool isSelected, bool isCore)
        {
            if (!_isDarkTheme)
            {
                lbl.Background = new SolidColorBrush(isSelected ? Color.FromRgb(0, 108, 190) : Color.FromRgb(240, 243, 246));
                lbl.BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(0, 152, 255) : Color.FromRgb(215, 220, 226));
                if (lbl.Child is TextBlock txt)
                {
                    txt.Foreground = new SolidColorBrush(isSelected ? Colors.White : (isCore ? Color.FromRgb(100, 105, 115) : Color.FromRgb(30, 35, 45)));
                }
            }
            else
            {
                lbl.Background = new SolidColorBrush(isSelected ? Color.FromRgb(0, 122, 204) : Color.FromRgb(55, 55, 58));
                lbl.BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(0, 152, 255) : Color.FromRgb(85, 85, 85));
                if (lbl.Child is TextBlock txt)
                {
                    txt.Foreground = new SolidColorBrush(isSelected ? Colors.White : (isCore ? Color.FromRgb(180, 180, 180) : Color.FromRgb(245, 245, 245)));
                }
            }
        }

        private void Chip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Border chip = sender as Border;
            bool isCore = wpCore.Children.Contains(chip);

            if (isCore) { if (selectedCoreTag != null) StyleChip(selectedCoreTag, false, true); selectedCoreTag = chip; StyleChip(selectedCoreTag, true, true); }
            else { if (selectedCustomTag != null) StyleChip(selectedCustomTag, false, false); selectedCustomTag = chip; StyleChip(selectedCustomTag, true, false); }

            if (e.ClickCount == 2)
            {
                string cmd = chip.Tag.ToString();
                if (isCore && MessageBox.Show($"警告：移除核心命令 [{cmd}] 可能导致无法正常切出中文。\n您确定要强行移除吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                ExecuteDelete(cmd, isCore);
            }
        }

        private void FilterTagsVisibility()
        {
            string filter = txtSearch.Text.Trim().ToUpper();
            foreach (Border ctrl in wpCustom.Children) ctrl.Visibility = string.IsNullOrEmpty(filter) || ctrl.Tag.ToString().Contains(filter) ? Visibility.Visible : Visibility.Collapsed;
            foreach (Border ctrl in wpCore.Children) ctrl.Visibility = string.IsNullOrEmpty(filter) || ctrl.Tag.ToString().Contains(filter) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExecuteDelete(string cmd, bool isCore)
        {
            List<string> cmdList = isCore ? coreCmds : customCmds;
            int idx = cmdList.IndexOf(cmd);
            undoStack.Push(new UndoAction { Command = cmd, IsCore = isCore, OriginalIndex = idx }); redoStack.Clear();
            cmdList.Remove(cmd);
            if (!isCore) customCmdSet.Remove(cmd);
            RebuildTags(isCore ? wpCore : wpCustom, cmdList, isCore);
            ShowToastStatus($"✅ 指令 [{cmd}] 已移除，可按 Ctrl+Z 撤销。");
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string input = txtNewCmd.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(input)) return;

            string[] multiCmds = input.Split(new char[] { ' ', ',', ';', '；', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int addedCount = 0; int dupCount = 0;

            foreach (string cmdRaw in multiCmds)
            {
                string cmd = cmdRaw.Trim('\uFEFF', '\u200B').Trim();
                if (string.IsNullOrEmpty(cmd)) continue;
                if (coreCmdSet.ContainsKey(cmd) || customCmdSet.ContainsKey(cmd)) { dupCount++; continue; }

                customCmds.Insert(0, cmd);
                customCmdSet[cmd] = true;
                addedCount++;
            }

            if (addedCount > 0)
            {
                txtNewCmd.Clear(); txtNewCmd.Focus();
                RebuildTags(wpCustom, customCmds, false);
                ShowToastStatus($"✅ 成功添加 {addedCount} 个指令" + (dupCount > 0 ? $" (跳过 {dupCount} 个重复)" : ""));
            }
            else if (dupCount > 0) ShowToastStatus($"⚠ 输入的指令已存在，无需重复添加", true);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && txtNewCmd.IsFocused) { BtnAdd_Click(null, null); e.Handled = true; return; }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (txtNewCmd.IsFocused || txtSearch.IsFocused) return;
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
                    RebuildTags(last.IsCore ? wpCore : wpCustom, cmdList, last.IsCore);
                    ShowToastStatus($"✅ 已撤销删除 [{last.Command}]");
                }
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                if (txtNewCmd.IsFocused || txtSearch.IsFocused) return;
                if (redoStack.Count > 0)
                {
                    UndoAction last = redoStack.Pop(); undoStack.Push(last);
                    List<string> cmdList = last.IsCore ? coreCmds : customCmds;
                    cmdList.Remove(last.Command);
                    if (!last.IsCore) customCmdSet.Remove(last.Command);
                    RebuildTags(last.IsCore ? wpCore : wpCustom, cmdList, last.IsCore);
                    ShowToastStatus($"✅ 已重做删除 [{last.Command}]");
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (txtNewCmd.IsFocused || txtSearch.IsFocused) return;
                if (selectedCustomTag != null) ExecuteDelete(selectedCustomTag.Tag.ToString(), false);
                else if (selectedCoreTag != null && MessageBox.Show($"确定移除核心指令 [{selectedCoreTag.Tag}] 吗？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    ExecuteDelete(selectedCoreTag.Tag.ToString(), true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) { this.Close(); }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("是否恢复默认配置？\n(这将会把系统核心命令和预置外挂命令全部恢复，覆盖当前的修改)", "恢复默认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                coreCmds = new List<string>(UiConfigManager.DefaultCommands);
                customCmds = new List<string>(UiConfigManager.InitialPluginCommands);
                customCmdSet.Clear();
                foreach (var c in customCmds) customCmdSet[c] = true;
                undoStack.Clear(); redoStack.Clear();

                RbProcess.IsChecked = true;
                RebuildTags(wpCore, coreCmds, true);
                RebuildTags(wpCustom, customCmds, false);
                ShowToastStatus("✅ 已成功恢复所有系统默认配置");
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            List<string> finalCmds = new List<string>(coreCmds);
            finalCmds.AddRange(customCmds);
            UiConfigManager.SaveAllCommands(finalCmds, RbGlobal.IsChecked == true);
            this.DialogResult = true;
            this.Close();
        }

        private void ShowToastStatus(string message, bool isWarning = false)
        {
            lblStatus.Text = message;
            lblStatus.Foreground = new SolidColorBrush(isWarning ? Color.FromRgb(255, 107, 107) : Color.FromRgb(0, 229, 255));
            lblStatus.Visibility = Visibility.Visible;
            statusTimer.Stop(); statusTimer.Start();
        }

        private void LblCheckUpdate_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string latestVer = lblCheckUpdate.Tag != null ? lblCheckUpdate.Tag.ToString() : "最新版";
            if (MessageBox.Show($"发现全新版本 [{latestVer}]！\n是否立即前往 GitHub 下载页查看更新内容？", "发现新版本", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo(AppConstants.LatestReleaseUrl) { UseShellExecute = true }); } catch { }
            }
        }

        private void LoadIconDynamically()
        {
            try
            {
                string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                Uri iconUri = new Uri($"pack://application:,,,/{assemblyName};component/Resources/icon.ico", UriKind.Absolute);
                var bmp = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
                this.Icon = bmp;
                if (imgLogo != null) imgLogo.Source = bmp;
                return;
            }
            catch { }

            try
            {
                string dllDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dllDir))
                {
                    string localIco = System.IO.Path.Combine(dllDir, "icon.ico");
                    if (System.IO.File.Exists(localIco))
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage(new Uri(localIco, UriKind.Absolute));
                        this.Icon = bmp;
                        if (imgLogo != null) imgLogo.Source = bmp;
                    }
                }
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) { this.Close(); }
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSearchWatermark != null)
            {
                txtSearchWatermark.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            FilterTagsVisibility();
        }
        private void TxtNewCmd_KeyDown(object sender, KeyEventArgs e) { }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
    }
}