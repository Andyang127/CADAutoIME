using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;

namespace OpenCadIme.Core
{
    public class CommandInterceptor : IDisposable
    {
        private Dictionary<string, CommandCategory> _whitelistCommands;
        private List<Document> _hookedDocs = new List<Document>();
        private Dictionary<Document, CommandCategory> _textCommandActiveDocs = new Dictionary<Document, CommandCategory>();

        private readonly object _docLock = new object();
        private readonly object _textCmdLock = new object();
        private bool _disposed = false;

        public event EventHandler CommandStateChanged;

        public CommandInterceptor(Dictionary<string, CommandCategory> initialWhitelist)
        {
            _whitelistCommands = initialWhitelist ?? new Dictionary<string, CommandCategory>();

            try
            {
                foreach (Document doc in Application.DocumentManager) AttachEvents(doc);
                Application.DocumentManager.DocumentCreated += OnDocumentCreated;
                Application.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
                Application.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            }
            catch (Exception ex) { Logger.Error("CommandInterceptor", "初始化图纸生命周期监听失败", ex); }
        }

        public void UpdateWhitelist(Dictionary<string, CommandCategory> newWhitelist)
        {
            _whitelistCommands = newWhitelist ?? new Dictionary<string, CommandCategory>();
            Logger.Info("CommandInterceptor", $"白名单已更新，当前数量: {_whitelistCommands.Count}");
        }

        public CommandCategory GetActiveCommandCategory()
        {
            try
            {
                if (Application.DocumentManager.Count == 0) return CommandCategory.None;

                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null || doc.IsDisposed) return CommandCategory.None;

                // 核心地面真值比对：优先以 AutoCAD 原生 CMDNAMES 系统变量为准，彻底防止第三方命令状态残留
                object cmdVar = null;
                try { cmdVar = Application.GetSystemVariable("CMDNAMES"); } catch { }
                string activeCmds = cmdVar != null ? cmdVar.ToString().Trim() : string.Empty;

                if (string.IsNullOrEmpty(activeCmds))
                {
                    // CAD 当前无任何运行中命令（完全空闲），彻底清空任何历史残留
                    lock (_textCmdLock)
                    {
                        _textCommandActiveDocs[doc] = CommandCategory.None;
                    }
                    return CommandCategory.None;
                }

                // 检查当前实际运行的命令是否匹配白名单
                string[] cmds = activeCmds.Split('\'');
                for (int i = cmds.Length - 1; i >= 0; i--)
                {
                    string c = cmds[i].Trim();
                    if (!string.IsNullOrEmpty(c))
                    {
                        CommandCategory cat = GetCommandCategoryFast(c);
                        if (cat != CommandCategory.None)
                        {
                            lock (_textCmdLock)
                            {
                                _textCommandActiveDocs[doc] = cat;
                            }
                            return cat;
                        }
                    }
                }

                // 若当前实际运行的命令（如 LINE, REC, REGEN 等）均非白名单文本命令，直接置为 None
                lock (_textCmdLock)
                {
                    _textCommandActiveDocs[doc] = CommandCategory.None;
                }
                return CommandCategory.None;
            }
            catch { return CommandCategory.None; }
        }

        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e) { if (e.Document != null) AttachEvents(e.Document); }

        private void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != null)
            {
                AttachEvents(e.Document);
            }
            CommandStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            lock (_docLock)
            {
                for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                {
                    Document doc = _hookedDocs[i];
                    try
                    {
                        if (doc == null || doc.IsDisposed || (e.Document != null && doc.UnmanagedObject == e.Document.UnmanagedObject))
                        {
                            _hookedDocs.RemoveAt(i);
                        }
                    }
                    catch
                    {
                        _hookedDocs.RemoveAt(i);
                    }
                }
            }
            CleanupDisposedDocStates();
        }

        private void AttachEvents(Document doc)
        {
            if (doc == null || doc.IsDisposed) return;
            lock (_docLock)
            {
                if (!_hookedDocs.Contains(doc))
                {
                    doc.CommandWillStart += OnCommandWillStart;
                    doc.CommandEnded += OnCommandEnded;
                    doc.CommandCancelled += OnCommandEnded;
                    doc.CommandFailed += OnCommandEnded;

                    doc.LispWillStart += OnLispWillStart;
                    doc.LispEnded += OnLispEnded;
                    doc.LispCancelled += OnLispEnded;

                    _hookedDocs.Add(doc);
                }
            }
        }

        private void DetachEventsUnsafe(Document doc)
        {
            try
            {
                if (doc == null || doc.IsDisposed) return;
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandEnded;
                doc.CommandFailed -= OnCommandEnded;

                doc.LispWillStart -= OnLispWillStart;
                doc.LispEnded -= OnLispEnded;
                doc.LispCancelled -= OnLispEnded;
            }
            catch (Exception ex) { Logger.Error("CommandInterceptor", $"注销事件失败", ex); }
        }

        private void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            try
            {
                string cmdName = string.Empty;
                try
                {
                    cmdName = e.GlobalCommandName;
                    Logger.Info("CommandInterceptor", $"抓取到底层命令触发: {cmdName}");
                }
                catch
                {
                    cmdName = string.Empty;
                }

                if (string.IsNullOrEmpty(cmdName) || cmdName.StartsWith("'")) return;

                Document doc = sender as Document;
                if (doc != null)
                {
                    CommandCategory cat = GetCommandCategoryFast(cmdName);
                    SetTextCommandActive(doc, cat);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("CommandInterceptor", "命令启动判定异常", ex);
            }
        }

        private void OnCommandEnded(object sender, CommandEventArgs e)
        {
            try
            {
                string cmdName = string.Empty;
                try
                {
                    cmdName = e.GlobalCommandName;
                }
                catch
                {
                    cmdName = string.Empty;
                }

                if (!string.IsNullOrEmpty(cmdName) && cmdName.StartsWith("'", StringComparison.Ordinal)) return;

                Document doc = sender as Document;
                if (doc != null) SetTextCommandActive(doc, CommandCategory.None);
            }
            catch (Exception ex) { Logger.Error("CommandInterceptor", "命令结束判定异常", ex); }
        }

        private void OnLispWillStart(object sender, LispWillStartEventArgs e)
        {
            try
            {
                string cmdName = e.FirstLine;
                if (string.IsNullOrEmpty(cmdName)) return;

                if (cmdName.StartsWith("(C:", StringComparison.OrdinalIgnoreCase))
                {
                    cmdName = cmdName.Substring(3).TrimEnd(')').Trim();
                    CommandCategory cat = GetCommandCategoryFast(cmdName);

                    Document doc = sender as Document;
                    if (doc != null) SetTextCommandActive(doc, cat);
                }
            }
            catch { }
        }

        private void OnLispEnded(object sender, EventArgs e)
        {
            try
            {
                Document doc = sender as Document;
                if (doc != null) SetTextCommandActive(doc, CommandCategory.None);
            }
            catch { }
        }

        private CommandCategory GetCommandCategoryFast(string rawCmd)
        {
            if (string.IsNullOrEmpty(rawCmd) || rawCmd.Trim().Length == 0) return CommandCategory.None;

            string normalized = NormalizeCommand(rawCmd);
            if (string.IsNullOrEmpty(normalized)) return CommandCategory.None;

            if (_whitelistCommands.ContainsKey(normalized))
                return _whitelistCommands[normalized];

            // 针对天正及第三方带前缀命令（如 T80_DHWZ、TCH_DHWZ 等）按基名匹配
            int underscoreIdx = normalized.IndexOf('_');
            if (underscoreIdx > 0 && underscoreIdx < normalized.Length - 1)
            {
                string stripped = normalized.Substring(underscoreIdx + 1);
                if (_whitelistCommands.ContainsKey(stripped))
                    return _whitelistCommands[stripped];
            }

            return CommandCategory.None;
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

        private void SetTextCommandActive(Document doc, CommandCategory category)
        {
            if (doc == null || doc.IsDisposed) return;
            bool stateChanged = false;
            lock (_textCmdLock)
            {
                _textCommandActiveDocs.TryGetValue(doc, out CommandCategory currentState);
                if (currentState != category)
                {
                    if (category == CommandCategory.None)
                    {
                        _textCommandActiveDocs.Remove(doc);
                    }
                    else
                    {
                        _textCommandActiveDocs[doc] = category;
                    }
                    stateChanged = true;
                }
            }
            if (stateChanged) CommandStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CleanupDisposedDocStates()
        {
            lock (_textCmdLock)
            {
                List<Document> toRemove = new List<Document>();
                foreach (var kvp in _textCommandActiveDocs)
                {
                    try
                    {
                        if (kvp.Key == null || kvp.Key.IsDisposed)
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }
                    catch
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var doc in toRemove) _textCommandActiveDocs.Remove(doc);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                try
                {
                    Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                    Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                    Application.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
                }
                catch { }

                lock (_docLock)
                {
                    for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                    {
                        if (_hookedDocs[i] != null && !_hookedDocs[i].IsDisposed)
                            DetachEventsUnsafe(_hookedDocs[i]);
                    }
                    _hookedDocs.Clear();
                }
                lock (_textCmdLock) { _textCommandActiveDocs.Clear(); }
            }
            catch
            {
            }
            finally { _disposed = true; }
        }
    }
}