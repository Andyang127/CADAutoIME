using System;

namespace OpenCadIme
{
    /// <summary>
    /// 全局共享常量
    /// 统一管理版本号、配置路径、注册表路径等全局常量，避免多处硬编码不同步
    /// </summary>
    internal static class AppConstants
    {
        // ==========================================
        // 版本信息 (未来升级，只需修改这 1 行！)
        // ==========================================
        /// <summary>主版本号（纯数字，用于语义版本比对）</summary>
        public const string Version = "0.4.0";

        /// <summary>带 v 前缀的版本号（用于 UI 显示）</summary>
        public const string VersionDisplay = "v" + Version;

        /// <summary>完整显示版本（带空格后缀，兼容原有格式）</summary>
        public const string VersionFull = Version + " ";

        // ==========================================
        // 配置文件
        // ==========================================
        /// <summary>配置文件名</summary>
        public const string ConfigFileName = "AutoImeCommands.txt";

        /// <summary>配置目录名（位于 %AppData% 下）</summary>
        public const string ConfigDirName = "OpenCadIme";

        /// <summary>配置文件版本号（用于配置格式升级迁移）</summary>
        public const int ConfigVersion = 3;

        // ==========================================
        // 注册表
        // ==========================================
        /// <summary>注册表路径（与命名空间一致，统一品牌）</summary>
        public const string RegistryPath = @"Software\OpenCadIme\CADAutoIme";

        // ==========================================
        // 更新相关 (未来若更换仓库，只需修改这 1 行！)
        // ==========================================
        /// <summary>GitHub 仓库地址</summary>
        public const string GitHubRepo = "Andyang127/CADAutoIME";

        /// <summary>最新发布页 URL</summary>
        public const string LatestReleaseUrl = "https://github.com/" + GitHubRepo + "/releases/latest";

        /// <summary>GitHub API 最新 release 接口</summary>
        public const string GitHubApiLatest = "https://api.github.com/repos/" + GitHubRepo + "/releases/latest";

        /// <summary>更新检测 User-Agent</summary>
        public const string UpdaterUserAgent = "CAD-Auto-IME-Updater";

        /// <summary>网络请求超时时间（毫秒）</summary>
        public const int NetworkTimeoutMs = 5000;

        // ==========================================
        // 作者信息
        // ==========================================
        /// <summary>作者/开发者 ID</summary>
        public const string AuthorName = "浅醉·墨语";

        /// <summary>插件完整名称</summary>
        public const string PluginFullName = "CAD Auto IME 输入法自动切换程序";

        /// <summary>插件简称</summary>
        public const string PluginShortName = "CAD Auto IME";
    }
}