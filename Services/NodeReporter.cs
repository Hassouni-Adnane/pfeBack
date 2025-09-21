using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace SignNowBackend.Services
{
    public sealed class NodeReporter
    {
        private readonly HttpClient _http;
        private readonly ILogger<NodeReporter> _log;
        private readonly string _url;
        private readonly string _secret;

        public NodeReporter(IHttpClientFactory factory, ILogger<NodeReporter> log)
        {
            _http = factory.CreateClient();
            _log = log;
            _url = Environment.GetEnvironmentVariable("NODE_API_URL") ?? "http://localhost:5000/api/documents";
            _secret = Environment.GetEnvironmentVariable("DOCUMENTS_WEBHOOK_SECRET") ?? "supersecret123";
        }

        public async Task ReportAsync(object payload, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-Api-Key", _secret);

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                _log.LogWarning("Node logging failed ({Status}): {Body}", (int)res.StatusCode, body);
        }
    }
}
