using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading.Tasks;

namespace WindowsEventLogMonitor;

internal class HttpService
{
    private static readonly HttpClient client = new HttpClient();
    private readonly Config config;

    public HttpService()
    {
        config = Config.GetCachedConfig() ?? new Config();
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        // 设置超时
        client.Timeout = TimeSpan.FromSeconds(config.Security.TimeoutSeconds);

        // 设置API密钥
        if (!string.IsNullOrEmpty(config.Security.ApiKey))
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.Security.ApiKey}");
        }

        // 设置用户代理
        client.DefaultRequestHeaders.Add("User-Agent", "WindowsEventLogMonitor/1.0");
    }

    public async Task PushLogsToAPIAsync(string jsonData, string apiUrl)
    {
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
            var testData = new { test = "connection", timestamp = DateTime.Now };
            var jsonData = System.Text.Json.JsonSerializer.Serialize(testData);

            using (var content = new StringContent(jsonData, Encoding.UTF8, "application/json"))
            {
                var response = await client.PostAsync(apiUrl, content).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
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

    public void Dispose()
    {
        client?.Dispose();
    }
}
