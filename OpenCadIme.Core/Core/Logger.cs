using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace OpenCadIme.Core
{
    internal static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppConstants.ConfigDirName),
            "CADAutoIME_Error.log"
        );

        private static readonly Queue<string> _logQueue = new Queue<string>();
        private static readonly object _queueLock = new object();
        private static bool _isFlushing = false;

        public static void Error(string module, string message, Exception ex = null)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{timestamp}] [ERROR] [{module}] {message}\r\n";
                if (ex != null)
                {
                    logEntry += $"  --> Exception: {ex.Message}\r\n";
                    logEntry += $"  --> StackTrace: {ex.StackTrace}\r\n";
                }
                logEntry += new string('-', 60) + "\r\n";

                Debug.WriteLine(logEntry);

                lock (_queueLock)
                {
                    _logQueue.Enqueue(logEntry);
                    if (!_isFlushing)
                    {
                        _isFlushing = true;
                        ThreadPool.QueueUserWorkItem(FlushLogQueue);
                    }
                }
            }
            catch { /* 终极防线静默 */ }
        }

        public static void Info(string module, string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Debug.WriteLine($"[{timestamp}] [INFO] [{module}] {message}\r\n");
            }
            catch { }
        }

        private static void FlushLogQueue(object state)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                while (true)
                {
                    List<string> batchToLog = new List<string>();
                    lock (_queueLock)
                    {
                        if (_logQueue.Count > 0)
                        {
                            while (_logQueue.Count > 0)
                            {
                                batchToLog.Add(_logQueue.Dequeue());
                            }
                        }
                        else
                        {
                            _isFlushing = false;
                            return;
                        }
                    }

                    if (batchToLog.Count > 0)
                    {
                        using (StreamWriter sw = new StreamWriter(LogFilePath, true, System.Text.Encoding.UTF8))
                        {
                            foreach (string entry in batchToLog)
                            {
                                sw.Write(entry); 
                            }
                        }
                    }
                }
            }
            catch
            {
                lock (_queueLock) { _isFlushing = false; }
            }
        }
    }
}