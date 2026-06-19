using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OpenCadIme
{
    /// <summary>
    /// 配置管理器 - 兼容 CAD 2007-2027 全版本
    /// </summary>
    public static class ConfigManager
    {
        #region 常量定义
        public const int ConfigVersion = 1;
        private const string ConfigFileName = "AutoImeCommands.txt";
        private static readonly StringComparer CommandComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly object _fileLock = new object();
        #endregion

        #region 默认命令定义
        public static readonly string[] DefaultCommands = {
            "DIM", "DIMLINEAR", "DIMALIGNED", "DIMANGULAR", "DIMRADIUS", "DIMDIAMETER",
            "DIMORDINATE", "DIMBASELINE", "DIMCONTINUE",
            "QLEADER", "LEADER", "MLEADER", "MLEADEREDIT", "MLEADERSTYLE", "MLEADERCONTENTEDIT",
            "DIMEDIT", "DIMTEDIT", "TOLERANCE","CLASSICINSERT", "MINSERT",
            "ATTDEF", "-ATTDEF", "ATTEDIT", "-ATTEDIT", "EATTEDIT", "BATTMAN", "ATTREDEF", "ATTSYNC",
            "BLOCK", "-BLOCK", "BEDIT", "REFEDIT", "REFCLOSE", "WBLOCK", "INSERT", "-INSERT",
            "BTABLE", "BACTION", "BPARAMETER", "BVISSTATES","TABLE", "TABLEDIT", "TABLEEXPORT", "FIELD",
            "SAVE", "SAVEAS", "QSAVE", "EXPORT", "OPEN", "NEW", "PLOT", "PUBLISH","RENAME", "PURGE",
            "LAYER", "CLASSICLAYER", "STYLE", "TABLESTYLE", "DIMSTYLE", "MLSTYLE", "GROUP",
            "HATCH", "HATCHEDIT", "DBLCLKEDIT", "TOBJEDIT"
        };

        private static readonly string[] InitExtendCommands = {
            "DHSHR", "DHBJ", "WZSHR", "TYBJ", "YPZ", "XXBZH", "BGHZH", "AAPMGZ",
            "WZBH", "WZBG", "WZCD", "WZFH", "WZJX", "WZPL", "WZQX", "WZSZ", "WZTX", "WZZJ",
            "BZBH", "BZCD", "BZFH", "BZJX", "BZPL", "BZQX", "BZSZ", "BZTX", "BZZJ",
            "PMZ", "LMBZ", "PMMZ","GJBZ", "GJFH", "GJBH", "MSBZ", "JSBZ", "GJMC",
            "GXBZ", "SLBZ", "SBMC", "SMWZ","FKBZ", "SGBZ", 
            "YQ_WZBH", "YQ_WZPL", "YQ_WZTX", "YQ_WZBG", "YQ_BZBJ", "YQ_BZPL", "YQ_BZTX",
            "YQ_BGHZ", "YQ_YPZ", "YQ_MCBZ", "YQ_GJMC",
            "TS_GJBZ", "TS_GJBH", "TS_SMZ", "TS_JJS", "TS_GJMC", "TS_HFBZ"
        };
        #endregion

        #region 公共属性
        private static int _loadedCustomCount;
        public static int LoadedCustomCount
        {
            get { return _loadedCustomCount; }
            private set { _loadedCustomCount = value; }
        }
        #endregion

        #region 配置文件路径
        private static string GetConfigFilePath()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string pluginDir = Path.Combine(appData, "OpenCadIme");
                if (!Directory.Exists(pluginDir))
                {
                    Directory.CreateDirectory(pluginDir);
                }
                return Path.Combine(pluginDir, ConfigFileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetConfigFilePath 异常: " + ex.Message);
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            }
        }
        #endregion

        #region 加载配置
        public static Dictionary<string, bool> LoadCommands()
        {
            Dictionary<string, bool> commands = new Dictionary<string, bool>(CommandComparer);
            LoadedCustomCount = 0;

            try
            {
                lock (_fileLock)
                {
                    string configFilePath = GetConfigFilePath();
                    if (File.Exists(configFilePath))
                    {
                        LoadCommandsFromFile(configFilePath, commands);
                    }
                    else
                    {
                        InitializeDefaultConfig(commands);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadCommands 异常: " + ex.Message);
                InitializeDefaultConfig(commands);
            }

            return commands;
        }

        private static void LoadCommandsFromFile(string configFilePath, Dictionary<string, bool> commands)
        {
            using (FileStream fs = new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string cleanLine = line.Trim('\uFEFF', '\u200B').Trim();
                    if (string.IsNullOrEmpty(cleanLine)
                        || cleanLine.StartsWith("#", StringComparison.Ordinal)
                        || cleanLine.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    commands[cleanLine] = true;
                    LoadedCustomCount++;
                }
            }
        }

        private static void InitializeDefaultConfig(Dictionary<string, bool> commands)
        {
            List<string> initCommands = new List<string>(DefaultCommands);
            initCommands.AddRange(InitExtendCommands);
            SaveAllCommands(initCommands);

            foreach (string cmd in initCommands)
            {
                commands[cmd] = true;
            }
        }
        #endregion

        #region 读取与保存
        public static List<string> ReadAllCommandsFromDisk()
        {
            List<string> list = new List<string>();

            try
            {
                lock (_fileLock)
                {
                    string path = GetConfigFilePath();
                    if (!File.Exists(path)) return list;

                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string c = line.Trim('\uFEFF', '\u200B').Trim();
                            if (!string.IsNullOrEmpty(c) && !c.StartsWith("//", StringComparison.Ordinal))
                            {
                                list.Add(c.ToUpperInvariant());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadAllCommandsFromDisk 异常: " + ex.Message);
            }

            return list;
        }

        public static void SaveAllCommands(List<string> allCommands)
        {
            if (allCommands == null) throw new ArgumentNullException("allCommands");

            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        List<string> fileContent = new List<string>();
                        fileContent.Add("// ============================================================");
                        fileContent.Add("// CAD Auto IME 输入法自动切换 - 全量白名单配置 (版本 v" + ConfigVersion + ")");
                        fileContent.Add("// ============================================================");
                        fileContent.Add("// 你可以在此添加或删除需要保持中文输入法的命令（每行一个）");
                        fileContent.Add("// 字母不区分大小写。以 // 或 # 开头的行会被视为注释");
                        fileContent.Add("// 修改后请在 CAD 中运行 CUSTOMAUTOIME 命令重新加载");
                        fileContent.Add("// ============================================================");
                        fileContent.Add("");
                        fileContent.AddRange(allCommands);

                        string configPath = GetConfigFilePath();
                        string tempPath = configPath + ".tmp";

                        File.WriteAllLines(tempPath, fileContent.ToArray(), new UTF8Encoding(false));

                        // 修复 V0.3: 摒弃 Delete + Move 的危险写法，改用安全的 Copy 覆盖机制
                        File.Copy(tempPath, configPath, true);
                        File.Delete(tempPath);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1)
                    {
                        System.Windows.Forms.MessageBox.Show(
                            "保存配置失败，请检查文件是否被其他 CAD 进程占用。\n\n错误详情：" + ex.Message,
                            "保存错误", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                    Thread.Sleep(200); // 避让其他进程
                }
            }
        }
        #endregion
    }
}