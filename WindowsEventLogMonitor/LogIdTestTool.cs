using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowsEventLogMonitor
{
    /// <summary>
    /// 日志ID提取测试工具
    /// </summary>
    public static class LogIdTestTool
    {
        /// <summary>
        /// 测试日志ID提取功能
        /// </summary>
        public static void TestLogIdExtraction()
        {
            Console.WriteLine("=== 日志ID提取功能测试 ===");

            // 测试用例
            var testLines = new[]
            {
                "[2025-01-15 14:30:25] [启动时间: 2025-01-15 09:00:00] Log ID: 1073760088_1749477731_638851033310000000_MSSQLSERVER,Generated at: 2025-06-09 22:02:11,Pushed at: 2025-06-09 22:02:14",
                "[2025-01-15 14:30:26] [启动时间: 2025-01-15 09:00:00] Log ID: 1073744838_1749477731_638851033310000000_MSSQLSERVER,Generated at: 2025-06-09 22:02:11,Pushed at: 2025-06-09 22:02:15",
                "Log ID: 1073760278_1749477731_638851033310000000_MSSQLSERVER, Pushed at: 2025-06-09 22:02:16", // 旧格式
                "无效行测试",
                "",
                "[2025-01-15 14:30:27] [启动时间: 2025-01-15 09:00:00] Log ID: 1073760088_1749477707_638851033070000000_MSSQLSERVER"
            };

            var extractedIds = new HashSet<string>();

            foreach (var line in testLines)
            {
                Console.WriteLine($"测试行: {line}");
                var beforeCount = extractedIds.Count;

                // 调用LogFileManager的私有方法（通过反射）
                ExtractLogIdFromTestLine(line, extractedIds);

                if (extractedIds.Count > beforeCount)
                {
                    var newId = extractedIds.Last();
                    Console.WriteLine($"  ✓ 提取到ID: {newId}");
                }
                else
                {
                    Console.WriteLine($"  ✗ 未提取到ID");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"总共提取到 {extractedIds.Count} 个唯一ID:");
            foreach (var id in extractedIds)
            {
                Console.WriteLine($"  - {id}");
            }
        }

        /// <summary>
        /// 分析现有日志文件中的重复ID
        /// </summary>
        public static void AnalyzeDuplicateIds(string logType = "sql_server_push_log")
        {
            Console.WriteLine($"=== 分析 {logType} 中的重复ID ===");

            var allIds = new List<string>();
            var idCounts = new Dictionary<string, int>();

            try
            {
                // 检查最近3天的日志文件
                for (int i = 0; i < 3; i++)
                {
                    var date = DateTime.Now.AddDays(-i);
                    var logFilePath = LogFileManager.GetLogFilePathForDate(logType, date);

                    if (File.Exists(logFilePath))
                    {
                        Console.WriteLine($"分析文件: {logFilePath}");
                        var lines = File.ReadAllLines(logFilePath);

                        foreach (var line in lines)
                        {
                            var extractedIds = new HashSet<string>();
                            ExtractLogIdFromTestLine(line, extractedIds);

                            foreach (var id in extractedIds)
                            {
                                allIds.Add(id);
                                idCounts[id] = idCounts.ContainsKey(id) ? idCounts[id] + 1 : 1;
                            }
                        }
                    }
                }

                // 找出重复的ID
                var duplicateIds = idCounts.Where(kvp => kvp.Value > 1).ToList();

                Console.WriteLine($"总共找到 {allIds.Count} 条日志记录");
                Console.WriteLine($"唯一ID数量: {idCounts.Count}");
                Console.WriteLine($"重复ID数量: {duplicateIds.Count}");

                if (duplicateIds.Count > 0)
                {
                    Console.WriteLine("\n重复的ID及其出现次数:");
                    foreach (var kvp in duplicateIds.OrderByDescending(x => x.Value))
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value} 次");
                    }
                }
                else
                {
                    Console.WriteLine("✓ 没有发现重复的ID");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"分析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理重复的日志记录
        /// </summary>
        public static void CleanupDuplicateRecords(string logType = "sql_server_push_log")
        {
            Console.WriteLine($"=== 清理 {logType} 中的重复记录 ===");

            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var date = DateTime.Now.AddDays(-i);
                    var logFilePath = LogFileManager.GetLogFilePathForDate(logType, date);

                    if (File.Exists(logFilePath))
                    {
                        var lines = File.ReadAllLines(logFilePath);
                        var uniqueLines = new List<string>();
                        var seenIds = new HashSet<string>();

                        Console.WriteLine($"处理文件: {logFilePath} ({lines.Length} 行)");

                        foreach (var line in lines)
                        {
                            var extractedIds = new HashSet<string>();
                            ExtractLogIdFromTestLine(line, extractedIds);

                            if (extractedIds.Count == 0)
                            {
                                // 没有ID的行直接保留
                                uniqueLines.Add(line);
                            }
                            else
                            {
                                var id = extractedIds.First();
                                if (!seenIds.Contains(id))
                                {
                                    seenIds.Add(id);
                                    uniqueLines.Add(line);
                                }
                            }
                        }

                        if (uniqueLines.Count < lines.Length)
                        {
                            // 备份原文件
                            var backupPath = logFilePath + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            File.Copy(logFilePath, backupPath);
                            Console.WriteLine($"原文件已备份到: {backupPath}");

                            // 写入去重后的内容
                            File.WriteAllLines(logFilePath, uniqueLines);
                            Console.WriteLine($"✓ 从 {lines.Length} 行减少到 {uniqueLines.Count} 行，删除了 {lines.Length - uniqueLines.Count} 条重复记录");
                        }
                        else
                        {
                            Console.WriteLine("✓ 该文件没有重复记录");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试版本的日志ID提取方法
        /// </summary>
        private static void ExtractLogIdFromTestLine(string line, HashSet<string> pushedLogIds)
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
    }
}