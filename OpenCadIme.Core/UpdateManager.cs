#pragma warning disable SYSLIB0014
using System;
using System.Net;
using System.Threading;

namespace OpenCadIme.Core
{
    public static class UpdateManager
    {
        public static void CheckForUpdates(Action<string> onUpdateAvailable)
        {
            CheckForUpdates((ver, _) => onUpdateAvailable?.Invoke(ver), null);
        }

        public static void CheckForUpdates(Action<string, bool> onUpdateAvailable, string skippedPrereleaseVersion)
        {
            // 独立后台线程，彻底避免 DNS 阻塞导致线程池枯竭假死
            Thread updateThread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(AppConstants.GitHubApiLatest);
                    request.Method = "GET";
                    request.Timeout = AppConstants.NetworkTimeoutMs;
                    request.UserAgent = AppConstants.UpdaterUserAgent;

                    using (var response = request.GetResponse())
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        string latestRaw = AppConstants.Version;

                        int tagKeyIdx = json.IndexOf("\"tag_name\"");
                        if (tagKeyIdx >= 0)
                        {
                            int valStart = json.IndexOf('"', tagKeyIdx + 10) + 1;
                            int valEnd = json.IndexOf('"', valStart);
                            if (valStart > 0 && valEnd > valStart)
                            {
                                latestRaw = json.Substring(valStart, valEnd - valStart).Trim();
                            }
                        }

                        string currentVer = AppConstants.Version;
                        string currentNum = ExtractNumericVersion(currentVer);
                        string latestNum = ExtractNumericVersion(latestRaw);

                        Version current = ParseVersionSafe(currentNum);
                        Version latest = ParseVersionSafe(latestNum);

                        bool isPrerelease = IsPrereleaseTag(latestRaw);

                        if (latest != null && current != null && latest > current)
                        {
                            if (isPrerelease && IsVersionSkipped(latestRaw, skippedPrereleaseVersion))
                            {
                                return;
                            }
                            onUpdateAvailable?.Invoke(latestRaw, isPrerelease);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("UpdateManager", "后台检查更新失败", ex);
                }
            }));

            updateThread.IsBackground = true;
            updateThread.Priority = ThreadPriority.Lowest; // 最低优先级，坚决不抢占画图性能
            updateThread.Start();
        }

        #region 版本号解析工具方法

        private static string ExtractNumericVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "0.0.0.0";
            string clean = tag.Trim().TrimStart('v', 'V');
            int dashIndex = clean.IndexOf('-');
            if (dashIndex > 0) clean = clean.Substring(0, dashIndex);
            return clean;
        }

        private static bool IsPrereleaseTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            string clean = tag.Trim().TrimStart('v', 'V');
            return clean.Contains("-");
        }

        private static Version ParseVersionSafe(string versionStr)
        {
            try
            {
                if (string.IsNullOrEmpty(versionStr)) return null;
                string[] parts = versionStr.Split('.');
                if (parts.Length < 2) versionStr = versionStr + ".0.0";
                else if (parts.Length < 3) versionStr = versionStr + ".0";
                return new Version(versionStr);
            }
            catch { return null; }
        }

        private static bool IsVersionSkipped(string latestTag, string skippedVersion)
        {
            if (string.IsNullOrEmpty(skippedVersion)) return false;
            string a = latestTag.Trim().TrimStart('v', 'V').ToLowerInvariant();
            string b = skippedVersion.Trim().TrimStart('v', 'V').ToLowerInvariant();
            return a == b;
        }

        #endregion
    }
}