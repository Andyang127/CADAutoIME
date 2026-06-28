using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenCadIme; // 引用 AppConstants

namespace OpenCadIme.UI
{
    /// <summary>
    /// UI 界面专属配置管理器，负责提供默认命令集与原始文本的磁盘读写
    /// </summary>
    public static class UiConfigManager
    {
        // 【核心修复】：使用嵌套的 Path.Combine，兼容 .NET Framework 2.0/3.5 (CAD2007-2011)
        public static readonly string ConfigPath = Path.Combine(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppConstants.ConfigDirName),
            AppConstants.ConfigFileName
        );

        public static bool IsGlobalMode { get; set; } = false;

        public static readonly string[] DefaultCommands = {
            "TEXT", "DTEXT", "MTEXT", "_MTEXT", "MTEDIT", "DDEDIT", "FIND",
            "ATTDEF", "_ATTDEF", "ATTEDIT", "_ATTEDIT", "EATTEDIT", "BATTMAN", "ATTREDEF",
            "QLEADER", "LEADER", "MLEADER", "MLEADERCONTENTEDIT",
            "TABLE", "TABLEDIT", "TOBJEDIT","BEDIT",
            "SAVEAS", "EXPORT", "WBLOCK", "TABLEEXPORT", "OPEN", "NEW", "PUBLISH", "SAVE", "QSAVE",
            "BLOCK", "_BLOCK", "BMAKE", "RENAME", "_RENAME", "STYLE", "LAYER", "_LAYER", "DIMSTYLE", "GROUP",
            "PLOT", "PAGESETUP", "QSELECT", "FILTER", "HATCH", "BHATCH","_TEXTEDIT"
        };

        public static readonly string[] InitialPluginCommands = {
            "DHWZ", "TMBZ", "ZFBZ", "YCBZ", "SYTM", "FJMC", "WDNAM",
            "GJMC", "JSBZ", "ZWBZ", "SMBZ", "TBLKNAME", "TKGM", "GGWZ",
            "WZYS", "QXWZ", "TS_SMZ", "TS_GJMC", "TS_JJS", "TS_HFBZ",
            "YQ_WZPL", "YQ_BZBJ", "YQ_BZPL", "YQ_YPZ", "YQ_MCBZ", "YQ_GJMC",
            "DD", "AF", "AT", "AB", "ABB", "IVT_TextEdit", "IVT_TextSerial",
            "IVT_AttEdit", "IVT_BlockRename", "YX_CK", "YX_MJ", "YX_WT",
            "DHSHR", "DHBJ", "WZSHR", "TYBJ", "WZPL", "BZPL",
            "YPZ", "XXBZH", "PMZ", "LMBZ", "PMMZ", "SBMC", "SMWZ"
        };

        public static List<string> ReadAllCommandsFromDisk()
        {
            List<string> cmds = new List<string>();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    // 允许系统内多个 CAD 多开并发读写
                    using (FileStream fs = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string tLine = line.Trim();
                            if (string.IsNullOrEmpty(tLine) || tLine.StartsWith("//") || tLine.StartsWith("[")) continue;
                            string normalized = NormalizeCommand(tLine);
                            if (!string.IsNullOrEmpty(normalized)) cmds.Add(normalized);
                        }
                    }
                }
            }
            catch { /* 遇到极端死锁时静默，返回空，让主程序使用默认字典兜底 */ }
            return cmds;
        }

        private static string NormalizeCommand(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Trim().Length == 0) return string.Empty;
            string cmd = input.Trim().Trim('\uFEFF', '\u200B').ToUpperInvariant();
            while (cmd.Length > 0 && (cmd[0] == '_' || cmd[0] == '-' || cmd[0] == '\'' || cmd[0] == '.'))
            {
                cmd = cmd.Substring(1);
            }
            return cmd;
        }

        public static void SaveAllCommands(List<string> commands, bool isGlobal)
        {
            IsGlobalMode = isGlobal;
            string dir = Path.GetDirectoryName(ConfigPath);
            int retries = 3;

            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { return; }

            // 增加重试机制和文件防并发独占锁
            while (retries > 0)
            {
                try
                {
                    using (FileStream fs = new FileStream(ConfigPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        sw.WriteLine($"// {AppConstants.PluginShortName} 自定义配置文件 ({AppConstants.VersionDisplay})");
                        sw.WriteLine("// 警告：请勿修改 [Mode] 标签，否则可能导致设置丢失");
                        sw.WriteLine(isGlobal ? "[Mode:Global]" : "[Mode:Process]");
                        sw.WriteLine("// ==========================================");
                        foreach (string cmd in commands)
                        {
                            if (!string.IsNullOrEmpty(cmd) && cmd.Trim().Length > 0)
                            {
                                sw.WriteLine(cmd.Trim().ToUpperInvariant());
                            }
                        }
                    }
                    break;
                }
                catch (IOException)
                {
                    retries--;
                    if (retries == 0) return;
                    System.Threading.Thread.Sleep(150);
                }
                catch
                {
                    break;
                }
            }
        }

        public static void SaveAllCommands(List<string> commands)
        {
            SaveAllCommands(commands, IsGlobalMode);
        }
    }
}