using System.Threading.Tasks;
using WindowsEventLogMonitor;

namespace WindowsEventLogMonitor.Tests;

/// <summary>
/// P0/P1 修复的回归测试
/// </summary>
public class AuditFixTests
{
    /// <summary>
    /// P0-3: UseHttps=true 时 http:// 应升级为 https://
    /// </summary>
    [Theory]
    [InlineData("http://172.16.32.160:55000/api/aa/test", true, "https://172.16.32.160:55000/api/aa/test")]
    [InlineData("http://example.com/api", true, "https://example.com/api")]
    [InlineData("HTTP://example.com/api", true, "https://example.com/api")]
    public void NormalizeUrl_UseHttpsTrue_UpgradesHttpToHttps(string url, bool useHttps, string expected)
    {
        var result = HttpService.NormalizeUrl(url, useHttps);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// P0-3: UseHttps=true 时 https:// 应保持不变
    /// </summary>
    [Theory]
    [InlineData("https://172.16.32.160:55000/api/aa/test", true, "https://172.16.32.160:55000/api/aa/test")]
    [InlineData("https://example.com/api", true, "https://example.com/api")]
    public void NormalizeUrl_AlreadyHttps_StaysHttps(string url, bool useHttps, string expected)
    {
        var result = HttpService.NormalizeUrl(url, useHttps);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// P0-3: UseHttps=false 时 http:// 应保持不变（不强制升级）
    /// </summary>
    [Theory]
    [InlineData("http://172.16.32.160:55000/api/aa/test", false, "http://172.16.32.160:55000/api/aa/test")]
    [InlineData("https://example.com/api", false, "https://example.com/api")]
    public void NormalizeUrl_UseHttpsFalse_KeepsOriginal(string url, bool useHttps, string expected)
    {
        var result = HttpService.NormalizeUrl(url, useHttps);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// P0-3: 空 URL 不应抛异常
    /// </summary>
    [Theory]
    [InlineData("", true, "")]
    [InlineData(null, true, null)]
    public void NormalizeUrl_EmptyOrNull_ReturnsAsIs(string? url, bool useHttps, string? expected)
    {
        var result = HttpService.NormalizeUrl(url!, useHttps);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// P1-7: 去重存储添加新 ID 应成功
    /// </summary>
    [Fact]
    public void DedupStore_TryAdd_NewId_ReturnsTrue()
    {
        var store = new InMemoryDeduplicationStore();
        Assert.True(store.TryAdd("log_1"));
        Assert.True(store.Contains("log_1"));
        Assert.Equal(1, store.Count);
    }

    /// <summary>
    /// P1-7: 去重存储添加重复 ID 应失败
    /// </summary>
    [Fact]
    public void DedupStore_TryAdd_DuplicateId_ReturnsFalse()
    {
        var store = new InMemoryDeduplicationStore();
        Assert.True(store.TryAdd("log_1"));
        Assert.False(store.TryAdd("log_1"));
        Assert.Equal(1, store.Count);
    }

    /// <summary>
    /// P1-7: 去重存储空/空白 ID 应拒绝
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DedupStore_TryAdd_InvalidId_ReturnsFalse(string? invalidId)
    {
        var store = new InMemoryDeduplicationStore();
        Assert.False(store.TryAdd(invalidId!));
        Assert.Equal(0, store.Count);
    }

    /// <summary>
    /// P1-7: 去重存储超过容量时按 FIFO 淘汰最早记录（修复内存无限增长）
    /// </summary>
    [Fact]
    public void DedupStore_ExceedsCapacity_EvictsOldestFIFO()
    {
        var store = new InMemoryDeduplicationStore(capacity: 3);
        store.TryAdd("log_1");
        store.TryAdd("log_2");
        store.TryAdd("log_3");
        Assert.Equal(3, store.Count);
        Assert.True(store.Contains("log_1"));

        // 添加第4条，应淘汰最早的 log_1
        store.TryAdd("log_4");
        Assert.Equal(3, store.Count);
        Assert.False(store.Contains("log_1"), "log_1 应被 FIFO 淘汰");
        Assert.True(store.Contains("log_2"));
        Assert.True(store.Contains("log_3"));
        Assert.True(store.Contains("log_4"));
    }

    /// <summary>
    /// P1-7: 去重存储容量下限保护（不应小于1）
    /// </summary>
    [Fact]
    public void DedupStore_CapacityBelowMinimum_ClampedTo1()
    {
        var store = new InMemoryDeduplicationStore(capacity: 0);
        Assert.Equal(1, store.Capacity);
    }

    /// <summary>
    /// P1-7: 去重存储 Clear 应清空所有记录
    /// </summary>
    [Fact]
    public void DedupStore_Clear_RemovesAll()
    {
        var store = new InMemoryDeduplicationStore();
        store.TryAdd("log_1");
        store.TryAdd("log_2");
        Assert.Equal(2, store.Count);

        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.False(store.Contains("log_1"));
    }

    /// <summary>
    /// P1-6: Config 并发获取缓存配置不应抛异常（线程安全）
    /// </summary>
    [Fact]
    public async Task Config_GetCachedConfig_ConcurrentAccess_IsThreadSafe()
    {
        // 并发从多线程访问 GetCachedConfig，验证加锁后不抛异常
        var exceptions = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var cfg = Config.GetCachedConfig();
                        Assert.NotNull(cfg);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                }
            });
        }
        await Task.WhenAll(tasks);
        Assert.Empty(exceptions);
    }
}
