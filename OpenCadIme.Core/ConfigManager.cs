using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace OpenCadIme
{
    public static class ConfigManager
    {
        public static int LoadedCustomCount { get; private set; } = 0;

        // 系统内置核心默认命令
        public static readonly string[] DefaultCommands = {
            "DIM", "DIMLINEAR", "DIMALIGNED", "DIMANGULAR", "DIMRADIUS", "DIMDIAMETER", "DIMORDINATE", "DIMBASELINE", "DIMCONTINUE",
            "QLEADER", "LEADER", "MLEADER", "MLEADEREDIT", "MLEADERSTYLE", "MLEADERCONTENTEDIT", "DIMEDIT", "DIMTEDIT", "TOLERANCE",
            "ATTDEF", "-ATTDEF", "ATTEDIT", "-ATTEDIT", "EATTEDIT", "BATTMAN", "ATTREDEF", "ATTSYNC",
            "BLOCK", "-BLOCK", "BEDIT", "REFEDIT", "REFCLOSE", "WBLOCK", "INSERT", "-INSERT", "CLASSICINSERT", "MINSERT",
            "BTABLE", "BACTION", "BPARAMETER", "BVISSTATES","TABLE", "TABLEDIT", "TABLEEXPORT", "FIELD",
            "SAVE", "SAVEAS", "QSAVE", "EXPORT", "OPEN", "NEW", "PLOT", "PUBLISH","RENAME", "PURGE",
            "LAYER", "CLASSICLAYER", "STYLE", "TABLESTYLE", "DIMSTYLE", "MLSTYLE", "GROUP","HATCH", "HATCHEDIT", "DBLCLKEDIT", "TOBJEDIT"
        };

        // 初次生成配置文件时附带的天正/源泉/探索者扩展命令
        private static readonly string[] InitExtendCommands = {
            "DHSHR", "DHBJ", "WZSHR", "TYBJ", "YPZ", "XXBZH", "BGHZH", "AAPMGZ","WZBH", "WZBG", "WZCD", "WZFH", "WZJX", "WZPL", "WZQX", "WZSZ", "WZTX", "WZZJ","BZBH", "BZCD", "BZFH", "BZJX", "BZPL", "BZQX", "BZSZ", "BZTX", "BZZJ","PMZ", "LMBZ", "PMMZ",
            "GJBZ", "GJFH", "GJBH", "MSBZ", "JSBZ", "GJMC",
            "GXBZ", "SLBZ", "SBMC", "SMWZ",
            "FKBZ", "SGBZ", "SBMC", "SMWZ",
            "YQ_WZBH", "YQ_WZPL", "YQ_WZTX", "YQ_WZBG","YQ_BZBJ", "YQ_BZPL", "YQ_BZTX","YQ_BGHZ", "YQ_YPZ", "YQ_MCBZ", "YQ_GJMC",
            "TS_GJBZ", "TS_GJBH", "TS_SMZ", "TS_JJS", "TS_GJMC", "TS_HFBZ"
        };

        /// <summary>获取配置文件完整路径</summary>
        private static string GetConfigFilePath()
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string sysDirPath = Path.GetDirectoryName(dllPath);
            DirectoryInfo parentDir = Directory.GetParent(sysDirPath);
            string rootPath = parentDir != null ? parentDir.FullName : sysDirPath;
            return Path.Combine(rootPath, "AutoImeCommands.txt");
        }

        /// <summary>加载全部命令，区分核心/自定义</summary>
        public static Dictionary<string, bool> LoadCommands()
        {
            Dictionary<string, bool> commands = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            LoadedCustomCount = 0;
            try
            {
                string configFilePath = GetConfigFilePath();
                if (File.Exists(configFilePath))
                {
                    using (FileStream fs = new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string cleanLine = line.Trim().ToUpper();
                            if (!string.IsNullOrEmpty(cleanLine) && !cleanLine.StartsWith("#") && !cleanLine.StartsWith("//"))
                            {
                                commands[cleanLine] = true;
                                LoadedCustomCount++;
                            }
                        }
                    }
                }
                else
                {
                    List<string> initCommands = new List<string>(DefaultCommands);
                    initCommands.AddRange(InitExtendCommands);
                    SaveAllCommands(initCommands);
                    foreach (string cmd in initCommands) commands[cmd] = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadCommands 异常: " + ex.Message);
            }
            return commands;
        }

        /// <summary>仅读取磁盘原始配置文本，不分核心自定义</summary>
        public static List<string> ReadAllCommandsFromDisk()
        {
            List<string> list = new List<string>();
            try
            {
                string path = GetConfigFilePath();
                if (File.Exists(path))
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string c = line.Trim().ToUpper();
                            if (!string.IsNullOrEmpty(c) && !c.StartsWith("//")) list.Add(c);
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

        /// <summary>全量覆盖保存命令配置</summary>
        public static void SaveAllCommands(List<string> allCommands)
        {
            try
            {
                List<string> fileContent = new List<string> {
                    "// CAD Auto IME 输入法自动切换 - 全量白名单配置",
                    "// 你可以在此添加或删除需要保持中文输入法的命令（每行一个）",
                    "// 字母不区分大小写。以 // 开头的行会被视为注释",
                    "// --------------------------------------------------"
                };
                fileContent.AddRange(allCommands);
                File.WriteAllLines(GetConfigFilePath(), fileContent.ToArray(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("保存配置失败，请检查文件是否被占用。\n" + ex.Message, "保存错误", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}