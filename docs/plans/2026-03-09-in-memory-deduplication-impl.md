# In-Memory Deduplication Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace file-based deduplication with in-memory HashSet storage to solve unlimited file growth problem.

**Architecture:** Create `InMemoryDeduplicationStore` class using thread-safe HashSet for O(1) deduplication lookups. Integrate with existing `SqlServerLogMonitor` to filter duplicates before API push.

**Tech Stack:** C# 12, .NET 8, System.Collections.Generic.HashSet, lock for thread-safety

---

## Task 1: Create InMemoryDeduplicationStore Class

**Files:**
- Create: `WindowsEventLogMonitor/InMemoryDeduplicationStore.cs`

**Step 1: Write the implementation**

```csharp
using System;
using System.Collections.Generic;

namespace WindowsEventLogMonitor
{
    /// <summary>
    /// 内存中去重存储 - 使用HashSet存储已推送的日志ID
    /// 服务重启后数据会丢失，允许少量重复推送
    /// </summary>
    public class InMemoryDeduplicationStore
    {
        private readonly HashSet<string> _pushedLogIds;
        private readonly object _lock;

        public InMemoryDeduplicationStore()
        {
            _pushedLogIds = new HashSet<string>();
            _lock = new object();
        }

        /// <summary>
        /// 尝试添加日志ID
        /// </summary>
        /// <param name="logId">日志唯一ID</param>
        /// <returns>true=新ID已添加, false=ID已存在</returns>
        public bool TryAdd(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId))
                return false;

            lock (_lock)
            {
                return _pushedLogIds.Add(logId);
            }
        }

        /// <summary>
        /// 检查日志ID是否已存在
        /// </summary>
        public bool Contains(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId))
                return false;

            lock (_lock)
            {
                return _pushedLogIds.Contains(logId);
            }
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _pushedLogIds.Clear();
            }
        }

        /// <summary>
        /// 当前存储的ID数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _pushedLogIds.Count;
                }
            }
        }
    }
}
```

**Step 2: Verify file compiles**

Run: `dotnet build WindowsEventLogMonitor/WindowsEventLogMonitor.csproj`
Expected: Build succeeds with no errors

**Step 3: Commit**

```bash
git add WindowsEventLogMonitor/InMemoryDeduplicationStore.cs
git commit -m "feat: add InMemoryDeduplicationStore for log deduplication

- Thread-safe HashSet-based storage
- O(1) lookup performance
- Service restart clears data (acceptable per requirements)"
```

---

## Task 2: Integrate Deduplication into SqlServerLogMonitor

**Files:**
- Modify: `WindowsEventLogMonitor/SqlServerLogMonitor.cs:14-30` (add field)
- Modify: `WindowsEventLogMonitor/SqlServerLogMonitor.cs:30-45` (initialize in constructor)
- Modify: `WindowsEventLogMonitor/SqlServerLogMonitor.cs:260-295` (filter in GetNewSQLServerLogsByTimeRangeAsync)
- Modify: `WindowsEventLogMonitor/SqlServerLogMonitor.cs:353-375` (track in ProcessLogsInBatchesAsync)

**Step 1: Add deduplication store field**

```csharp
public class SqlServerLogMonitor : IDisposable
{
    private readonly EventLogReader applicationLogReader;
    private readonly EventLogReader securityLogReader;
    private readonly HttpService httpService;
    private readonly InMemoryDeduplicationStore _dedupStore;  // NEW
    // ... rest of fields
}
```

**Step 2: Initialize in constructor**

```csharp
public SqlServerLogMonitor()
{
    applicationLogReader = new EventLogReader("Application");
    securityLogReader = new EventLogReader("Security");
    httpService = new HttpService();
    _dedupStore = new InMemoryDeduplicationStore();  // NEW
    // ... rest of initialization
}
```

**Step 3: Filter duplicates in GetNewSQLServerLogsByTimeRangeAsync**

Find the two places where `newLogs.Add()` is called (around line 282 and 321).

For each log entry, check if already pushed:

```csharp
var uniqueKey = GenerateUniqueKey(log);

// Skip if already pushed in this session
if (_dedupStore.Contains(uniqueKey))
{
    Console.WriteLine($"Skipping duplicate log: {uniqueKey}");
    continue;
}

newLogs.Add(new SqlServerLogEntry
{
    UniqueKey = uniqueKey,
    // ... rest of properties
});
```

**Step 4: Track successful pushes in ProcessLogsInBatchesAsync**

After successful push, add IDs to store:

```csharp
private async Task<bool> ProcessLogsInBatchesAsync(List<SqlServerLogEntry> logs, string apiUrl, int batchSize = 10)
{
    bool allBatchesSuccessful = true;

    for (int i = 0; i < logs.Count; i += batchSize)
    {
        var batch = logs.Skip(i).Take(batchSize).ToList();
        var json = JsonConvert.SerializeObject(batch, Formatting.None);

        try
        {
            await httpService.PushLogsToAPIAsync(json, apiUrl);
            Console.WriteLine($"成功推送 {batch.Count} 条SQL Server日志");

            // Track successfully pushed logs
            foreach (var log in batch)
            {
                _dedupStore.TryAdd(log.UniqueKey);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"推送日志失败: {ex.Message}");
            allBatchesSuccessful = false;
        }
    }

    return allBatchesSuccessful;
}
```

**Step 5: Add logging for deduplication stats**

In `CollectAndPushSQLServerLogsAsync`, add:

```csharp
if (sqlServerLogs.Count > 0)
{
    Console.WriteLine($"收集到 {sqlServerLogs.Count} 条新的SQL Server日志 (已去重存储: {_dedupStore.Count} 条)");
    // ... rest
}
```

**Step 6: Build and verify**

Run: `dotnet build WindowsEventLogMonitor/WindowsEventLogMonitor.csproj`
Expected: Build succeeds with no errors

**Step 7: Commit**

```bash
git add WindowsEventLogMonitor/SqlServerLogMonitor.cs
git commit -m "feat: integrate InMemoryDeduplicationStore into SqlServerLogMonitor

- Filter duplicates before adding to newLogs list
- Track successfully pushed logs in dedup store
- Add logging for deduplication statistics"
```

---

## Task 3: Clean Up Legacy File-Based Code

**Files:**
- Modify: `WindowsEventLogMonitor/LogFileManager.cs:96-114` (simplify LoadPushedLogIds)

**Step 1: Simplify LoadPushedLogIds**

Since we no longer use file-based deduplication, simplify this method:

```csharp
/// <summary>
/// 从指定类型的所有日志文件中加载已推送的日志ID
/// 注意：现使用内存去重，此方法保留仅用于兼容性
/// </summary>
public static HashSet<string> LoadPushedLogIds(string logType, int daysBack = 3)
{
    // 已改用内存去重存储，返回空集合
    return new HashSet<string>();
}
```

**Step 2: Remove unused legacy file checking code (optional cleanup)**

If desired, can also remove `CheckLegacyLogFiles`, `CheckLegacyTimeFiles`, `CleanupLegacyLogFiles` - but keep for now to minimize changes.

**Step 3: Build and verify**

Run: `dotnet build WindowsEventLogMonitor/WindowsEventLogMonitor.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add WindowsEventLogMonitor/LogFileManager.cs
git commit -m "refactor: simplify LoadPushedLogIds for memory-based deduplication

- Return empty set since deduplication now handled in memory
- Retain legacy compatibility methods"
```

---

## Task 4: Update Documentation

**Files:**
- Modify: `WindowsEventLogMonitor/重复日志问题解决方案.md`

**Step 1: Update documentation**

Add a section at the top explaining the new behavior:

```markdown
## 更新说明 (2026-03-09)

程序现已改用**内存去重**机制：
- 服务运行期间自动去重，无需配置文件
- 服务重启后允许少量重复（设计如此）
- 不再生成 `sql_server_push_log_*.ini` 文件

如需查看去重统计，可在程序日志中看到：
```
收集到 5 条新的SQL Server日志 (已去重存储: 1250 条)
```
```

**Step 2: Commit**

```bash
git add "WindowsEventLogMonitor/重复日志问题解决方案.md"
git commit -m "docs: update deduplication documentation

- Explain new in-memory deduplication mechanism
- Clarify behavior on service restart"
```

---

## Task 5: Final Verification

**Step 1: Full build**

Run: `dotnet build WindowsEventLogMonitor/WindowsEventLogMonitor.csproj -c Release`
Expected: Build succeeds with no warnings

**Step 2: Check git status**

Run: `git status`
Expected: All changes committed, working tree clean

**Step 3: Show summary**

Run: `git log --oneline -5`
Expected: See all 4 commits from this plan

---

## Summary of Changes

| File | Change |
|------|--------|
| `InMemoryDeduplicationStore.cs` | NEW - Thread-safe in-memory deduplication |
| `SqlServerLogMonitor.cs` | MODIFIED - Integrate dedup store into collection flow |
| `LogFileManager.cs` | MODIFIED - Simplify legacy file loading |
| `重复日志问题解决方案.md` | MODIFIED - Update documentation |

## Rollback Plan

If issues occur:
1. Revert commits: `git revert HEAD~3..HEAD`
2. Original file-based deduplication logic remains in `LogFileManager` as compatibility layer
