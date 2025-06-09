using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace WindowsEventLogMonitor
{
    /// <summary>
    /// 日志文件管理器 - 负责管理按日期分割的日志文件
    /// </summary>
    public static class LogFileManager
    {
        private static DateTime? _startupTime = null;

        /// <summary>
        /// 获取或设置程序启动时间
        /// </summary>
        public static DateTime StartupTime
        {
            get
            {
                if (_startupTime == null)
                {
                    _startupTime = DateTime.Now;
                    RecordStartupTime();
                }
                return _startupTime.Value;
            }
            private set => _startupTime = value;
        }

        /// <summary>
        /// 初始化日志文件管理器
        /// </summary>
        public static void Initialize()
        {
            // 记录启动时间
            StartupTime = DateTime.Now;

            // 清理旧的日志文件
            CleanupOldLogFiles();
        }

        /// <summary>
        /// 获取指定类型的当前日志文件路径
        /// </summary>
        /// <param name="logType">日志类型 (如 "push_log", "sql_server_push_log")</param>
        /// <returns>当前日期的日志文件路径</returns>
        public static string GetCurrentLogFilePath(string logType)
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd");
            return $"{logType}_{dateString}.ini";
        }

        /// <summary>
        /// 获取指定日期的日志文件路径
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="date">指定日期</param>
        /// <returns>指定日期的日志文件路径</returns>
        public static string GetLogFilePathForDate(string logType, DateTime date)
        {
            var dateString = date.ToString("yyyy-MM-dd");
            return $"{logType}_{dateString}.ini";
        }

        /// <summary>
        /// 写入日志条目
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="logEntry">日志条目内容</param>
        public static void WriteLogEntry(string logType, string logEntry)
        {
            // 已完全禁用日志记录
        }

        /// <summary>
        /// 异步写入日志条目
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="logEntry">日志条目内容</param>
        public static async System.Threading.Tasks.Task WriteLogEntryAsync(string logType, string logEntry)
        {
            // 已完全禁用日志记录
            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// 从指定类型的所有日志文件中加载已推送的日志ID
        /// 由于已禁用.ini文件记录，此方法现在返回空集合
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="daysBack">向前查找的天数，默认3天</param>
        /// <returns>已推送的日志ID集合</returns>
        public static HashSet<string> LoadPushedLogIds(string logType, int daysBack = 3)
        {
            // 由于已禁用.ini文件记录，返回空集合
            // 这将导致可能重复推送某些日志，但避免程序出错
            var pushedLogIds = new HashSet<string>();

            try
            {
                // 仍然检查旧的日志文件格式（兼容性）
                CheckLegacyLogFiles(logType, pushedLogIds);
            }
            catch
            {
                // 忽略异常
            }

            return pushedLogIds;
        }

        /// <summary>
        /// 获取最后处理的时间
        /// 由于已禁用.ini文件记录，此方法现在返回最小时间值
        /// </summary>
        /// <param name="logType">日志类型</param>
        /// <param name="daysBack">向前查找的天数</param>
        /// <returns>最后处理的时间</returns>
        public static DateTime GetLastProcessedTime(string logType, int daysBack = 3)
        {
            DateTime maxProcessedTime = DateTime.MinValue;
            string pattern = @"Generated at: (?<generatedTime>[\d\-/\s:]+)";

            try
            {
                // 由于已禁用.ini文件记录，只检查旧格式文件
                CheckLegacyTimeFiles(logType, ref maxProcessedTime, pattern);
            }
            catch
            {
                // 忽略异常
            }

            return maxProcessedTime;
        }

        /// <summary>
        /// 清理超过指定天数的旧日志文件
        /// </summary>
        /// <param name="retentionDays">保留天数，默认3天</param>
        public static void CleanupOldLogFiles(int retentionDays = 3)
        {
            try
            {
                // 只清理旧的单一日志文件（兼容性）
                CleanupLegacyLogFiles();
            }
            catch
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// 记录程序启动时间
        /// </summary>
        private static void RecordStartupTime()
        {
            // 已禁用启动时间记录
        }

        /// <summary>
        /// 从日志行中提取日志ID
        /// </summary>
        private static void ExtractLogIdFromLine(string line, HashSet<string> pushedLogIds)
        {
            try
            {
                // 查找 "Log ID: " 标记
                var logIdMarker = "Log ID: ";
                var logIdIndex = line.IndexOf(logIdMarker);
                if (logIdIndex >= 0)
                {
                    var startIndex = logIdIndex + logIdMarker.Length;
                    var remainingText = line.Substring(startIndex);

                    // 找到第一个逗号，日志ID在逗号之前
                    var commaIndex = remainingText.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        var logId = remainingText.Substring(0, commaIndex).Trim();
                        if (!string.IsNullOrWhiteSpace(logId))
                        {
                            pushedLogIds.Add(logId);
                        }
                    }
                    else
                    {
                        // 如果没有逗号，整个剩余部分就是日志ID
                        var logId = remainingText.Trim();
                        if (!string.IsNullOrWhiteSpace(logId))
                        {
                            pushedLogIds.Add(logId);
                        }
                    }
                }
                else
                {
                    // 尝试旧格式的解析方式（兼容性）
                    var parts = line.Split(',');
                    if (parts.Length > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.Contains("Log ID:"))
                        {
                            var logIdPart = firstPart.Split(':');
                            if (logIdPart.Length > 1)
                            {
                                var logId = logIdPart[logIdPart.Length - 1].Trim();
                                if (!string.IsNullOrWhiteSpace(logId))
                                {
                                    pushedLogIds.Add(logId);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略解析错误的行
            }
        }

        /// <summary>
        /// 检查旧格式的日志文件（兼容性）
        /// </summary>
        private static void CheckLegacyLogFiles(string logType, HashSet<string> pushedLogIds)
        {
            var legacyFile = logType + ".ini";
            if (File.Exists(legacyFile))
            {
                try
                {
                    var lines = File.ReadAllLines(legacyFile);
                    foreach (var line in lines)
                    {
                        ExtractLogIdFromLine(line, pushedLogIds);
                    }
                }
                catch
                {
                    // 忽略旧文件读取错误
                }
            }
        }

        /// <summary>
        /// 检查旧格式文件的时间（兼容性）
        /// </summary>
        private static void CheckLegacyTimeFiles(string logType, ref DateTime maxProcessedTime, string pattern)
        {
            var legacyFile = logType + ".ini";
            if (File.Exists(legacyFile))
            {
                try
                {
                    var lines = File.ReadAllLines(legacyFile);
                    foreach (var line in lines)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
                        if (match.Success)
                        {
                            if (DateTime.TryParse(match.Groups["generatedTime"].Value, out var generatedDate))
                            {
                                if (generatedDate > maxProcessedTime)
                                {
                                    maxProcessedTime = generatedDate;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略旧文件读取错误
                }
            }
        }

        /// <summary>
        /// 清理旧的单一日志文件（兼容性）
        /// </summary>
        private static void CleanupLegacyLogFiles()
        {
            // 已禁用日志文件操作
        }
    }
}