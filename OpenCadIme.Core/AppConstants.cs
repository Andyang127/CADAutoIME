using System;

namespace OpenCadIme
{
    internal static class AppConstants
    {
        public const string Version = "0.4.1";
        public const string VersionDisplay = "v" + Version;
        public const string VersionFull = Version + " ";
        public const string ConfigFileName = "AutoImeCommands.txt";
        public const string ConfigDirName = "OpenCadIme";
        public const int ConfigVersion = 3;
        public const string RegistryPath = @"Software\OpenCadIme\CADAutoIme";
        public const string GitHubRepo = "Andyang127/CADAutoIME";
        public const string LatestReleaseUrl = "https://github.com/" + GitHubRepo + "/releases/latest";
        public const string GitHubApiLatest = "https://api.github.com/repos/" + GitHubRepo + "/releases/latest";
        public const string UpdaterUserAgent = "CAD-Auto-IME-Updater";
        public const int NetworkTimeoutMs = 5000;
        public const string AuthorName = "浅醉·墨语";
        public const string PluginFullName = "CAD Auto IME 输入法自动切换程序";
        public const string PluginShortName = "CAD Auto IME";
    }
}