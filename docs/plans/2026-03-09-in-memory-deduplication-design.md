# 内存去重存储设计方案

## 背景

当前系统使用 `sql_server_push_log.ini` 文件记录已推送的日志ID进行去重，但该文件会无限增长且没有有效清理机制，导致磁盘空间问题和性能下降。

## 目标

- 解决日志文件无限增长问题
- 保持有效的去重能力
- 简化实现，降低维护成本

## 需求约束

- **时间窗口**：仅当前运行会话内去重
- **重启行为**：服务重启后允许少量重复
- **日志量**：100-1000条/小时

## 设计方案

### 架构

```
┌─────────────────────────────────────────────────────────────┐
│                    SqlServerLogMonitor                        │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐      ┌─────────────────────────────┐   │
│  │  EventLogReader │─────▶│  InMemoryDeduplicationStore │   │
│  │  (读取日志)      │      │  (内存去重存储)               │   │
│  └─────────────────┘      └─────────────────────────────┘   │
│                                    │                        │
│                                    ▼                        │
│                           ┌─────────────────┐               │
│                           │   HashSet<ID>   │               │
│                           │  (已推送ID集合)  │               │
│                           └─────────────────┘               │
└─────────────────────────────────────────────────────────────┘
```

### 核心组件

#### InMemoryDeduplicationStore

```csharp
public class InMemoryDeduplicationStore
{
    private readonly HashSet<string> _pushedLogIds = new();
    private readonly object _lock = new();

    /// <summary>
    /// 尝试添加日志ID，返回是否为新ID（true=新ID，false=已存在）
    /// </summary>
    public bool TryAdd(string logId)

    /// <summary>
    /// 检查日志ID是否已存在
    /// </summary>
    public bool Contains(string logId)

    /// <summary>
    /// 清空所有记录
    /// </summary>
    public void Clear()

    /// <summary>
    /// 当前存储的ID数量
    /// </summary>
    public int Count { get; }
}
```

### 数据流

1. 收集SQL Server日志
2. 生成UniqueKey (`InstanceId_Timestamp_Ticks_Source`)
3. 检查 `InMemoryDeduplicationStore`
   - 已存在 → 跳过
   - 不存在 → 继续推送
4. 推送日志到API
5. 推送成功 → 添加到 `InMemoryDeduplicationStore`

### 错误处理

- **推送失败**：不添加到存储，下次会重试
- **内存不足**：HashSet自动扩容，预估100-1000条/小时的量不会导致问题
- **服务重启**：存储随进程结束而清空，允许重复推送（符合需求）

### 内存估算

- 每条日志ID约50-80字符（约100字节）
- 1000条/小时 × 24小时 = 24,000条
- 内存占用约 2-3 MB，完全可接受

## 与其他方案对比

| 方案 | 优点 | 缺点 | 适用性 |
|------|------|------|--------|
| 内存HashSet | O(1)性能，无磁盘问题，实现简单 | 重启后数据丢失 | ★★★ 最佳匹配 |
| LRU缓存 | 内存可控 | 实现复杂，过度设计 | ★★☆ |
| SQLite内存 | 结构化查询 | 引入依赖，过于复杂 | ★☆☆ |

## 测试策略

1. 单元测试：验证去重逻辑正确性
2. 集成测试：模拟多次收集，验证重复日志被过滤
3. 性能测试：验证1000条/小时的场景下无性能问题

## 兼容性

- 保留 `LogFileManager` 现有接口（已禁用）
- 新存储类独立存在，不影响现有代码
- 回滚方案：可直接切换回文件存储（不推荐）

## 实施分支

基于 `feature/autostart-management` 分支开发
