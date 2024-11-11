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
    public async Task PushLogsToAPI(string jsonData, string apiUrl)
    {
        using (HttpClient client = new HttpClient())
        {
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(apiUrl, content);
            response.EnsureSuccessStatusCode();
        }
    }
}
