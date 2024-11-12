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

    public async Task PushLogsToAPIAsync(string jsonData, string apiUrl)
    {

        using (var content = new StringContent(jsonData, Encoding.UTF8, "application/json"))
        {
            try
            {
                var response = await client.PostAsync(apiUrl, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                // Potentially retry or throw agian after logging an error.
            }
            catch (Exception ex)
            {
                // decide what to do. Really simple does nothing.
            }
        }
    }


}
