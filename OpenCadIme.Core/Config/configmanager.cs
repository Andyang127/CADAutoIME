using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
namespace OpenCadIme
{
    public static class ConfigManager
    {
        // ⭐ v0.3.1 修复：版本号统一移至 AppConstants
        // public const int ConfigVersion = 3;
        // ⭐ v0.3.1 修复：配置文件名统一移至 AppConstants
        // private const string ConfigFileName = "AutoImeCommands.txt";
        private static readonly StringComparer CommandComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly object _fileLock = new object();

        /// <summary>
        /// ⭐ v0.3.1 新增：总命令数（包含默认内置命令）
        /// </summary>
        public static int LoadedTotalCount { get; private set; }

        /// <summary>
        /// ⭐ v0.3.1 语义修正：自定义命令数（仅用户添加的命令，不含默认内置命令）
        /// </summary>
        public static int LoadedCustomCount { get; private set; }
        public static readonly string[] DefaultCommands = {
            "TEXT", "DTEXT", "MTEXT", "-MTEXT", "MTEDIT", "DDEDIT", "FIND",
            "ATTDEF", "-ATTDEF", "ATTEDIT", "-ATTEDIT", "EATTEDIT", "BATTMAN", "ATTREDEF",
            "QLEADER", "LEADER", "MLEADER", "MLEADERCONTENTEDIT",
            "TABLE", "TABLEDIT", "TOBJEDIT",
            "SAVEAS", "EXPORT", "WBLOCK", "TABLEEXPORT", "OPEN", "NEW", "PUBLISH", "SAVE", "QSAVE",
            "BLOCK", "-BLOCK", "BMAKE", "RENAME", "-RENAME", "STYLE", "LAYER", "-LAYER", "DIMSTYLE", "GROUP",
            "PLOT", "PAGESETUP", "QSELECT", "FILTER", "HATCH", "BHATCH"
        };
        // 自定义命令
        public static readonly string[] InitialPluginCommands = {
            // ========== 天正系列（建筑/结构/机电标配） ==========
            "DHWZ", "TMBZ", "ZFBZ", "YCBZ", "SYTM", "FJMC", "WDNAM",
            "GJMC", "JSBZ", "ZWBZ", "SMBZ",
            "TBLKNAME", "TKGM", "GGWZ",
            "WZYS", "QXWZ",
            // ========== 探索者 TSSD（结构行业标杆） ==========
            "TS_SMZ", "TS_GJMC", "TS_JJS", "TS_HFBZ",
            // ========== 源泉设计 YQArch ==========
            "YQ_WZPL", "YQ_BZBJ", "YQ_BZPL", "YQ_YPZ", "YQ_MCBZ", "YQ_GJMC",
            // ========== 海龙工具箱（室内设计标配） ==========
            "DD", "AF", "AT", "AB", "ABB",
            // ========== 常青藤辅助工具 ==========
            "IVT_TextEdit", "IVT_TextSerial", "IVT_AttEdit", "IVT_BlockRename",
            // ========== 燕秀工具箱（机械模具标配） ==========
            "YX_CK", "YX_MJ", "YX_WT",
            // ========== 通用LISP/全插件通用命令 ==========
            "DHSHR", "DHBJ", "WZSHR", "TYBJ", "WZPL", "BZPL",
            "YPZ", "XXBZH", "PMZ", "LMBZ", "PMMZ", "SBMC", "SMWZ"
        };

        private static string GetConfigFilePath()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string pluginDir = Path.Combine(appData, AppConstants.ConfigDirName);
                if (!Directory.Exists(pluginDir)) Directory.CreateDirectory(pluginDir);
                return Path.Combine(pluginDir, AppConstants.ConfigFileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CADAutoIME] 获取路径异常: " + ex.Message);
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.ConfigFileName);
            }
        }
        public static bool IsSystemBuiltIn(string commandName)
        {
            string upperCmd = commandName.Trim().ToUpperInvariant();
            return Array.IndexOf(DefaultCommands, upperCmd) >= 0;
        }
        public static Dictionary<string, bool> LoadCommands()
        {
            Dictionary<string, bool> commands = new Dictionary<string, bool>(CommandComparer);
            LoadedTotalCount = 0;
            LoadedCustomCount = 0;
            try
            {
                lock (_fileLock)
                {
                    string configFilePath = GetConfigFilePath();
                    if (File.Exists(configFilePath))
                    {
                        string[] lines = File.ReadAllLines(configFilePath, new UTF8Encoding(false));
                        // ⭐ v0.3.1 修复：使用公共解析方法，统计总命令数和自定义命令数
                        List<string> parsedCommands = ParseCommandLines(lines);
                        foreach (string cmd in parsedCommands)
                        {
                            if (!commands.ContainsKey(cmd))
                            {
                                commands[cmd] = true;
                                LoadedTotalCount++;
                                // 语义修正：非系统内置命令才算自定义命令
                                if (!IsSystemBuiltIn(cmd))
                                {
                                    LoadedCustomCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        List<string> initialFullList = new List<string>(DefaultCommands);
                        initialFullList.AddRange(InitialPluginCommands);
                        SaveAllCommandsUnsafe(initialFullList);
                        foreach (var cmd in initialFullList)
                        {
                            string upperCmd = cmd.ToUpperInvariant();
                            if (!commands.ContainsKey(upperCmd))
                            {
                                commands[upperCmd] = true;
                                LoadedTotalCount++;
                                // 语义修正：非系统内置命令才算自定义命令
                                if (!IsSystemBuiltIn(upperCmd))
                                {
                                    LoadedCustomCount++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[CADAutoIME] 加载异常: " + ex.Message); }
            return commands;
        }

        /// <summary>
        /// ⭐ v0.3.1 新增：公共命令行解析方法，供 LoadCommands 和 ReadAllCommandsFromDisk 共用
        /// 去除 BOM、零宽空格、注释行，提取纯命令名
        /// </summary>
        private static List<string> ParseCommandLines(IEnumerable<string> lines)
        {
            List<string> result = new List<string>();
            if (lines == null) return result;

            foreach (string line in lines)
            {
                string cleanLine = line.Trim('\uFEFF', '\u200B').Trim();
                if (string.IsNullOrEmpty(cleanLine) || cleanLine.StartsWith("//")) continue;
                string[] parts = cleanLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string command = parts[0].ToUpperInvariant();
                    if (!string.IsNullOrEmpty(command))
                    {
                        result.Add(command);
                    }
                }
            }
            return result;
        }
        public static List<string> ReadAllCommandsFromDisk()
        {
            List<string> list = new List<string>();
            try
            {
                lock (_fileLock)
                {
                    string path = GetConfigFilePath();
                    if (!File.Exists(path)) return list;
                    // ⭐ v0.3.1 修复：使用公共解析方法，统一解析逻辑
                    List<string> allLines = new List<string>();
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            allLines.Add(line);
                        }
                    }
                    list = ParseCommandLines(allLines);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[CADAutoIME] 读取磁盘异常: " + ex.Message); }
            return list;
        }
        public static void SaveAllCommands(List<string> customCommands)
        {
            if (customCommands == null) return;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    lock (_fileLock) { SaveAllCommandsUnsafe(customCommands); }
                    break;
                }
                catch { Thread.Sleep(200); }
            }
        }
        private static void SaveAllCommandsUnsafe(List<string> commandsToWrite)
        {
            List<string> fileContent = new List<string>();
            fileContent.Add("// ============================================================");
            fileContent.Add("// CAD Auto IME 命令白名单配置");
            // ⭐ v0.3.1 新增：配置文件版本号，用于后续升级迁移
            fileContent.Add("// CONFIG_VERSION: " + AppConstants.ConfigVersion);
            fileContent.Add("// ============================================================");
            fileContent.Add("// 提示：列表中包含系统核心文字命令与常用第三方外挂命令。");
            fileContent.Add("// 您可以在此随意添加或删除。列表内的命令启动时，会自动切换为中文。");
            fileContent.Add("// 强烈建议保留 MTEXT, TEXT 等系统内置命令。");
            fileContent.Add("// ============================================================");
            fileContent.Add("");
            foreach (var cmd in commandsToWrite)
            {
                fileContent.Add(cmd.ToUpperInvariant());
            }
            string configPath = GetConfigFilePath();
            string tempPath = configPath + ".tmp";
            File.WriteAllLines(tempPath, fileContent.ToArray(), new UTF8Encoding(false));
            File.Copy(tempPath, configPath, true);
            File.Delete(tempPath);
        }
    }
}
