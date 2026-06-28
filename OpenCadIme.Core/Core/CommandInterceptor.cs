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
                Application.DocumentManager.DocumentDestroyed += OnDocumentDestroyed;
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
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null || doc.IsDisposed) return CommandCategory.None;

                lock (_textCmdLock)
                {
                    return _textCommandActiveDocs.TryGetValue(doc, out CommandCategory cat) ? cat : CommandCategory.None;
                }
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

        private void OnDocumentDestroyed(object sender, DocumentDestroyedEventArgs e)
        {
            lock (_docLock)
            {
                for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                {
                    Document doc = _hookedDocs[i];
                    try
                    {
                        if (doc == null || doc.IsDisposed)
                        {
                            DetachEventsUnsafe(doc);
                            _hookedDocs.RemoveAt(i);
                            continue;
                        }

                        if (!string.IsNullOrEmpty(e.FileName) && string.Equals(doc.Name, e.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            DetachEventsUnsafe(doc);
                            _hookedDocs.RemoveAt(i);
                        }
                    }
                    catch
                    {
                        DetachEventsUnsafe(doc);
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

                    // 完美级补充：支持原生 LISP 函数的输入法接管
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

                Document doc = sender as Document;
                if (doc != null)
                {
                    cmdName = cmdName.Replace("(C:", "").Replace("(c:", "").Replace(")", "").Trim();
                    CommandCategory cat = GetCommandCategoryFast(cmdName);
                    SetTextCommandActive(doc, cat);
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

            return _whitelistCommands.ContainsKey(normalized) ? _whitelistCommands[normalized] : CommandCategory.None;
        }

        private static string NormalizeCommand(string input)
        {
            string cmd = input.Trim().Trim('\uFEFF', '\u200B').ToUpperInvariant();
            while (cmd.Length > 0 && (cmd[0] == '_' || cmd[0] == '-' || cmd[0] == '\'' || cmd[0] == '.'))
            {
                cmd = cmd.Substring(1);
            }
            return cmd;
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
                Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                Application.DocumentManager.DocumentDestroyed -= OnDocumentDestroyed;

                lock (_docLock)
                {
                    for (int i = _hookedDocs.Count - 1; i >= 0; i--)
                    {
                        if (_hookedDocs[i] != null && !_hookedDocs[i].IsDisposed) DetachEventsUnsafe(_hookedDocs[i]);
                    }
                    _hookedDocs.Clear();
                }
                lock (_textCmdLock) { _textCommandActiveDocs.Clear(); }
            }
            catch (Exception ex)
            {
                Logger.Error("CommandInterceptor", "销毁资源时发生异常", ex);
            }
            finally { _disposed = true; }
        }
    }
}