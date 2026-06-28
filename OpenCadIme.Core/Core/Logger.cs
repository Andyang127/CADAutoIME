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

        // 使用内存队列缓冲日志，将磁盘 IO 操作剥离主线程
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
                    string entryToLog = null;
                    lock (_queueLock)
                    {
                        if (_logQueue.Count > 0)
                        {
                            entryToLog = _logQueue.Dequeue();
                        }
                        else
                        {
                            _isFlushing = false;
                            return; // 队列空了，退出后台线程
                        }
                    }

                    if (!string.IsNullOrEmpty(entryToLog))
                    {
                        // 这里在后台线程执行，哪怕硬盘慢如蜗牛，也绝不卡顿 CAD 界面
                        File.AppendAllText(LogFilePath, entryToLog);
                    }
                }
            }
            catch
            {
                // IO 写入失败，解锁标志位
                lock (_queueLock) { _isFlushing = false; }
            }
        }
    }
}