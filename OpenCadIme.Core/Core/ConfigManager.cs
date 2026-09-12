using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenCadIme.UI;

namespace OpenCadIme.Core
{
    public enum CommandCategory
    {
        None,
        Windowed,
        Inline
    }

    public static class ConfigManager
    {
        public static int LoadedCustomCount { get; private set; } = 0;

        private static readonly Dictionary<string, CommandCategory> DefaultCommandsMap = new Dictionary<string, CommandCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // 独立弹窗型
            {"MTEXT", CommandCategory.Windowed}, {"_MTEXT", CommandCategory.Windowed}, {"MTEDIT", CommandCategory.Windowed},
            {"DDEDIT", CommandCategory.Windowed}, {"_TEXTEDIT", CommandCategory.Windowed}, {"TEXTEDIT", CommandCategory.Windowed},
            {"DIMEDIT", CommandCategory.Windowed}, {"_DIMEDIT", CommandCategory.Windowed},
            {"DIMTEDIT", CommandCategory.Windowed}, {"_DIMTEDIT", CommandCategory.Windowed}, {"FIND", CommandCategory.Windowed},
            {"QLEADER", CommandCategory.Windowed}, {"LEADER", CommandCategory.Windowed}, {"MLEADER", CommandCategory.Windowed},
            {"TABLEDIT", CommandCategory.Windowed}, {"MLEADERCONTENTEDIT", CommandCategory.Windowed}, {"ATTDEF", CommandCategory.Windowed},
            {"_ATTDEF", CommandCategory.Windowed}, {"ATTEDIT", CommandCategory.Windowed}, {"_ATTEDIT", CommandCategory.Windowed},
            {"EATTEDIT", CommandCategory.Windowed}, {"BATTMAN", CommandCategory.Windowed}, {"ATTREDEF", CommandCategory.Windowed},
            {"TOBJEDIT", CommandCategory.Windowed},
            
            // 实时内联型
            {"TEXT", CommandCategory.Inline}, {"DTEXT", CommandCategory.Inline}, {"-TEXT", CommandCategory.Inline},
            {"RENAME", CommandCategory.Inline}, {"_RENAME", CommandCategory.Inline}, {"LAYER", CommandCategory.Inline},
            {"_LAYER", CommandCategory.Inline}, {"STYLE", CommandCategory.Inline}, {"BLOCK", CommandCategory.Inline},
            {"_BLOCK", CommandCategory.Inline}, {"BMAKE", CommandCategory.Inline}, {"DIMSTYLE", CommandCategory.Inline},
            {"GROUP", CommandCategory.Inline}, {"PLOT", CommandCategory.Inline}, {"PAGESETUP", CommandCategory.Inline},
            {"QSELECT", CommandCategory.Inline}, {"FILTER", CommandCategory.Inline}, {"HATCH", CommandCategory.Inline},
            {"BHATCH", CommandCategory.Inline}, {"SAVEAS", CommandCategory.Inline}, {"EXPORT", CommandCategory.Inline},
            {"WBLOCK", CommandCategory.Inline}, {"TABLEEXPORT", CommandCategory.Inline}, {"OPEN", CommandCategory.Inline},
            {"NEW", CommandCategory.Inline}, {"PUBLISH", CommandCategory.Inline}, {"SAVE", CommandCategory.Inline},
            {"QSAVE", CommandCategory.Inline},

            // 天正建筑及行业插件在位文本与标注命令（提升为 Windowed 独立在位文本类别）
            {"DHWZ", CommandCategory.Windowed}, {"_DHWZ", CommandCategory.Windowed},
            {"TTEXT", CommandCategory.Windowed}, {"_TTEXT", CommandCategory.Windowed},
            {"SMWZ", CommandCategory.Windowed}, {"_SMWZ", CommandCategory.Windowed},
            {"TMBZ", CommandCategory.Windowed}, {"_TMBZ", CommandCategory.Windowed},
            {"ZFBZ", CommandCategory.Windowed}, {"_ZFBZ", CommandCategory.Windowed},
            {"YCBZ", CommandCategory.Windowed}, {"_YCBZ", CommandCategory.Windowed},
            {"SYTM", CommandCategory.Windowed}, {"_SYTM", CommandCategory.Windowed},
            {"FJMC", CommandCategory.Windowed}, {"_FJMC", CommandCategory.Windowed},
            {"WDNAM", CommandCategory.Windowed}, {"_WDNAM", CommandCategory.Windowed},
            {"GJMC", CommandCategory.Windowed}, {"_GJMC", CommandCategory.Windowed},
            {"JSBZ", CommandCategory.Windowed}, {"_JSBZ", CommandCategory.Windowed},
            {"ZWBZ", CommandCategory.Windowed}, {"_ZWBZ", CommandCategory.Windowed},
            {"SMBZ", CommandCategory.Windowed}, {"_SMBZ", CommandCategory.Windowed},
            {"TBLKNAME", CommandCategory.Windowed}, {"_TBLKNAME", CommandCategory.Windowed},
            {"TKGM", CommandCategory.Windowed}, {"_TKGM", CommandCategory.Windowed},
            {"GGWZ", CommandCategory.Windowed}, {"_GGWZ", CommandCategory.Windowed},
            {"WZYS", CommandCategory.Windowed}, {"_WZYS", CommandCategory.Windowed},
            {"QXWZ", CommandCategory.Windowed}, {"_QXWZ", CommandCategory.Windowed},
            {"TMTEXT", CommandCategory.Windowed}, {"_TMTEXT", CommandCategory.Windowed},
            {"TCH_TEXT", CommandCategory.Windowed}, {"_TCH_TEXT", CommandCategory.Windowed},
            {"TCH_DHWZ", CommandCategory.Windowed}, {"_TCH_DHWZ", CommandCategory.Windowed},
            {"TCH_TMTEXT", CommandCategory.Windowed}, {"_TCH_TMTEXT", CommandCategory.Windowed},
            {"TCH_TTEXT", CommandCategory.Windowed}, {"_TCH_TTEXT", CommandCategory.Windowed},
            {"WZ", CommandCategory.Windowed}, {"_WZ", CommandCategory.Windowed},
            {"HZ", CommandCategory.Windowed}, {"_HZ", CommandCategory.Windowed},
            {"TXT", CommandCategory.Windowed}, {"_TXT", CommandCategory.Windowed},
            {"TT", CommandCategory.Windowed}, {"_TT", CommandCategory.Windowed},

            // 天正标注家族与改文字/改标注命令族 (Windowed 独立文本模式)
            {"ZDBZ", CommandCategory.Windowed}, {"_ZDBZ", CommandCategory.Windowed},
            {"TCH_ZDBZ", CommandCategory.Windowed}, {"_TCH_ZDBZ", CommandCategory.Windowed},
            {"GZDBZ", CommandCategory.Windowed}, {"_GZDBZ", CommandCategory.Windowed},
            {"GBZWZ", CommandCategory.Windowed}, {"_GBZWZ", CommandCategory.Windowed},
            {"GBZ", CommandCategory.Windowed}, {"_GBZ", CommandCategory.Windowed},
            {"GZDWZ", CommandCategory.Windowed}, {"_GZDWZ", CommandCategory.Windowed},
            {"GZMWZ", CommandCategory.Windowed}, {"_GZMWZ", CommandCategory.Windowed},
            {"TCH_DIMEDIT", CommandCategory.Windowed}, {"_TCH_DIMEDIT", CommandCategory.Windowed},
            {"TCH_TEXTEDIT", CommandCategory.Windowed}, {"_TCH_TEXTEDIT", CommandCategory.Windowed},
            {"TCH_DIMENSION", CommandCategory.Windowed}, {"_TCH_DIMENSION", CommandCategory.Windowed},
            {"BDBZ", CommandCategory.Windowed}, {"_BDBZ", CommandCategory.Windowed},
            {"KDBZ", CommandCategory.Windowed}, {"_KDBZ", CommandCategory.Windowed},
            {"DMBZ", CommandCategory.Windowed}, {"_DMBZ", CommandCategory.Windowed},
            {"PGBZ", CommandCategory.Windowed}, {"_PGBZ", CommandCategory.Windowed},
            {"JDBZ", CommandCategory.Windowed}, {"_JDBZ", CommandCategory.Windowed},
            {"HDBZ", CommandCategory.Windowed}, {"_HDBZ", CommandCategory.Windowed},
            {"BXBZ", CommandCategory.Windowed}, {"_BXBZ", CommandCategory.Windowed},
            {"ZBBZ", CommandCategory.Windowed}, {"_ZBBZ", CommandCategory.Windowed},
            {"DXBZ", CommandCategory.Windowed}, {"_DXBZ", CommandCategory.Windowed},
            {"TYBZ", CommandCategory.Windowed}, {"_TYBZ", CommandCategory.Windowed},
            {"CGBZ", CommandCategory.Windowed}, {"_CGBZ", CommandCategory.Windowed},
            {"QPBZ", CommandCategory.Windowed}, {"_QPBZ", CommandCategory.Windowed},

            // 图块改名、重命名、块定义与属性编辑命令族 (天正与原生)
            {"GMKM", CommandCategory.Windowed}, {"_GMKM", CommandCategory.Windowed},
            {"GKM", CommandCategory.Windowed}, {"_GKM", CommandCategory.Windowed},
            {"TCH_BLKNAME", CommandCategory.Windowed}, {"_TCH_BLKNAME", CommandCategory.Windowed},
            {"TBEDIT", CommandCategory.Windowed}, {"_TBEDIT", CommandCategory.Windowed},
            {"BEDIT", CommandCategory.Windowed}, {"_BEDIT", CommandCategory.Windowed},
            {"REFEDIT", CommandCategory.Windowed}, {"_REFEDIT", CommandCategory.Windowed},
            {"-RENAME", CommandCategory.Inline}, {"-BLOCK", CommandCategory.Inline},
            {"-ATTDEF", CommandCategory.Inline}, {"-ATTEDIT", CommandCategory.Inline}
        };

        private static readonly string[] TextPromptKeywords = new string[]
        {
            "块名", "名称", "文字", "文本", "说明", "注释", "标高", "轴号", "新值", "查找", "替换",
            "输入字符串", "重命名", "前缀", "后缀", "序号", "改名", "标头", "标注内容", "标题",
            "尺寸", "尺寸值", "标注", "公差",
            "name", "text", "string", "rename", "prefix", "suffix", "title", "content", "dim"
        };

        public static bool IsTextPromptMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;
            for (int i = 0; i < TextPromptKeywords.Length; i++)
            {
                if (msg.IndexOf(TextPromptKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static Dictionary<string, CommandCategory> LoadCommands()
        {
            Dictionary<string, CommandCategory> whitelist = new Dictionary<string, CommandCategory>(StringComparer.OrdinalIgnoreCase);

            try
            {
                LoadedCustomCount = 0;
                if (!File.Exists(UiConfigManager.ConfigPath))
                {
                    List<string> initial = new List<string>(UiConfigManager.DefaultCommands);
                    initial.AddRange(UiConfigManager.InitialPluginCommands);
                    UiConfigManager.SaveAllCommands(initial, false);
                }

                if (File.Exists(UiConfigManager.ConfigPath))
                {
                    using (FileStream fs = new FileStream(UiConfigManager.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string tLine = line.Trim();
                            if (string.IsNullOrEmpty(tLine) || tLine.StartsWith("//")) continue;

                            if (tLine.Equals("[Mode:Global]", StringComparison.OrdinalIgnoreCase)) { UiConfigManager.IsGlobalMode = true; continue; }
                            if (tLine.Equals("[Mode:Process]", StringComparison.OrdinalIgnoreCase)) { UiConfigManager.IsGlobalMode = false; continue; }

                            string cmd = NormalizeCommand(tLine);
                            // 修复：剔除了多余的重复判断逻辑
                            if (!string.IsNullOrEmpty(cmd) && !whitelist.ContainsKey(cmd))
                            {
                                whitelist[cmd] = DefaultCommandsMap.ContainsKey(cmd) ? DefaultCommandsMap[cmd] : CommandCategory.Inline;
                                LoadedCustomCount++;
                            }
                        }
                    }
                }

                foreach (var kvp in DefaultCommandsMap)
                {
                    if (!whitelist.ContainsKey(kvp.Key)) whitelist[kvp.Key] = kvp.Value;
                }
                return whitelist;
            }
            catch (Exception ex)
            {
                Logger.Error("ConfigManager", "核心白名单读取发生并发异常，回退到默认设置", ex);
                return new Dictionary<string, CommandCategory>(DefaultCommandsMap);
            }
        }

        public static bool IsTextCommand(string cmdName)
        {
            if (string.IsNullOrEmpty(cmdName)) return false;
            string norm = NormalizeCommand(cmdName);
            return DefaultCommandsMap.ContainsKey(norm);
        }

        private static string NormalizeCommand(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Trim().Length == 0) return string.Empty;
            string cmd = input.Trim('\uFEFF', '\u200B', ' ', '\t').ToUpperInvariant();
            int startIndex = 0;
            while (startIndex < cmd.Length && (cmd[startIndex] == '_' || cmd[startIndex] == '-' || cmd[startIndex] == '\'' || cmd[startIndex] == '.'))
            {
                startIndex++;
            }
            return startIndex > 0 ? cmd.Substring(startIndex) : cmd;
        }
    }
}