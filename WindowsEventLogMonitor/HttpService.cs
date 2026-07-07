using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace WindowsEventLogMonitor;

internal class HttpService
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly object initLock = new object();
    private static bool isInitialized = false;
    private Config config;

    public HttpService()
    {
        config = Config.GetCachedConfig() ?? new Config();
        ConfigureHttpClient();
    }

    /// <summary>
    /// 配置变更后重新应用 Header/Timeout（解决配置不刷新问题）
    /// </summary>
    public void RefreshConfig()
    {
        config = Config.GetCachedConfig() ?? new Config();
        lock (initLock)
        {
            // 重新应用超时
            client.Timeout = TimeSpan.FromSeconds(config.Security.TimeoutSeconds);

            // 重新应用 API 密钥
            client.DefaultRequestHeaders.Remove("Authorization");
            if (!string.IsNullOrEmpty(config.Security.ApiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.Security.ApiKey}");
            }

            // 重新应用用户代理
            client.DefaultRequestHeaders.Remove("User-Agent");
            client.DefaultRequestHeaders.Add("User-Agent", "WindowsEventLogMonitor/1.0");

            isInitialized = true;
        }
    }

    /// <summary>
    /// 根据 UseHttps 配置规范化 URL（强制将 http:// 升级为 https://）
    /// </summary>
    public static string NormalizeUrl(string apiUrl, bool useHttps)
    {
        if (string.IsNullOrEmpty(apiUrl))
            return apiUrl;

        if (useHttps && apiUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + apiUrl.Substring(7);
        }
        return apiUrl;
    }

    /// <summary>
    /// 使用当前配置规范化 URL
    /// </summary>
    private string NormalizeUrlWithConfig(string apiUrl)
    {
        var currentConfig = Config.GetCachedConfig() ?? config;
        return NormalizeUrl(apiUrl, currentConfig.Security.UseHttps);
    }

    private void ConfigureHttpClient()
    {
        if (isInitialized)
        {
            return;
        }

        lock (initLock)
        {
            if (isInitialized)
            {
                return;
            }

            // 设置超时
            client.Timeout = TimeSpan.FromSeconds(config.Security.TimeoutSeconds);

            // 设置API密钥 - 先移除可能存在的旧值再添加
            if (!string.IsNullOrEmpty(config.Security.ApiKey))
            {
                if (client.DefaultRequestHeaders.Contains("Authorization"))
                {
                    client.DefaultRequestHeaders.Remove("Authorization");
                }
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.Security.ApiKey}");
            }

            // 设置用户代理 - 先移除可能存在的旧值再添加
            if (client.DefaultRequestHeaders.Contains("User-Agent"))
            {
                client.DefaultRequestHeaders.Remove("User-Agent");
            }
            client.DefaultRequestHeaders.Add("User-Agent", "WindowsEventLogMonitor/1.0");

            isInitialized = true;
        }
    }

    public async Task PushLogsToAPIAsync(string jsonData, string apiUrl)
    {
        apiUrl = NormalizeUrlWithConfig(apiUrl);
        if (config.RetryPolicy.EnableRetry)
        {
            await PushLogsWithRetryAsync(jsonData, apiUrl);
        }
        else
        {
            await PushLogsOnceAsync(jsonData, apiUrl);
        }
    }

    private async Task PushLogsWithRetryAsync(string jsonData, string apiUrl)
    {
        int attempts = 0;
        Exception lastException = null;

        while (attempts <= config.RetryPolicy.MaxRetries)
        {
            try
            {
                await PushLogsOnceAsync(jsonData, apiUrl);
                return; // 成功，退出重试循环
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;

                if (attempts <= config.RetryPolicy.MaxRetries)
                {
                    Console.WriteLine($"推送失败，第 {attempts} 次重试 (最多 {config.RetryPolicy.MaxRetries} 次): {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(config.RetryPolicy.RetryDelaySeconds));
                }
            }
        }

        // 所有重试都失败了
        throw new Exception($"推送日志失败，已重试 {config.RetryPolicy.MaxRetries} 次。最后一次错误: {lastException?.Message}", lastException);
    }

    private async Task PushLogsOnceAsync(string jsonData, string apiUrl)
    {
        using (var content = new StringContent(jsonData, Encoding.UTF8, "application/json"))
        {
            try
            {
                var response = await client.PostAsync(apiUrl, content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"成功推送日志到 {apiUrl}，状态码: {response.StatusCode}");
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API返回错误状态码: {response.StatusCode}, 响应内容: {responseContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"HTTP请求异常: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                if (ex.InnerException is TimeoutException)
                {
                    throw new Exception($"请求超时 ({config.Security.TimeoutSeconds}秒): {ex.Message}", ex);
                }
                throw new Exception($"请求被取消: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"推送日志时发生未知错误: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 测试API连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(string apiUrl)
    {
        try
        {
            apiUrl = NormalizeUrlWithConfig(apiUrl);
            // 构建 health 端点 URL
            var baseUrl = apiUrl.TrimEnd('/');
            // 如果 URL 以 /api/aa/ 开头，使用 /api/aa/health
            var healthUrl = baseUrl.Contains("/api/aa/") && !baseUrl.EndsWith("/health")
                ? $"{baseUrl.Substring(0, baseUrl.IndexOf("/api/aa/") + 8)}health"
                : $"{baseUrl}/health";

            var response = await client.GetAsync(healthUrl).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取API服务器状态
    /// </summary>
    public async Task<string> GetApiStatusAsync(string baseUrl)
    {
        try
        {
            baseUrl = NormalizeUrlWithConfig(baseUrl);
            var statusUrl = $"{baseUrl.TrimEnd('/')}/status";
            var response = await client.GetAsync(statusUrl).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return $"状态检查失败: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            return $"无法获取状态: {ex.Message}";
        }
    }

}
