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
            {"DDEDIT", CommandCategory.Windowed}, {"_TEXTEDIT", CommandCategory.Windowed}, {"FIND", CommandCategory.Windowed},
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
            {"QSAVE", CommandCategory.Inline}
        };

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
                    // 【致命并发修复】：彻底废弃危险的 File.ReadAllLines，改用带 FileShare.ReadWrite 锁控制的流读取
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
                            if (!string.IsNullOrEmpty(cmd) && !whitelist.ContainsKey(cmd))
                            {
                                whitelist[cmd] = DefaultCommandsMap.ContainsKey(cmd) ? DefaultCommandsMap[cmd] : CommandCategory.Windowed;
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
    }
}