using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private const string SignNowBaseV1 = "https://api.signnow.com";
        private readonly HttpClient _http;
        private readonly ILogger<DownloadController> _log;

        public DownloadController(IHttpClientFactory httpClientFactory, ILogger<DownloadController> log)
        {
            _http = httpClientFactory.CreateClient();
            _log = log;
        }

        // GET /api/download/{documentId}
        // Proxies SignNow's document download to the browser.
        [HttpGet("{documentId}")]
        public async Task<IActionResult> Get(string documentId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                return Problem("Missing document id.", statusCode: 400);

            if (!TryGetBearer(out var token, out var badAuth))
                return badAuth;

            var url = $"{SignNowBaseV1}/document/{documentId}/download";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            _log.LogInformation("Downloading from SignNow: {DocId}", documentId);

            var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _log.LogWarning("SignNow download failed {Status}: {Body}", (int)res.StatusCode, body);
                return StatusCode((int)res.StatusCode, body);
            }

            var fileName =
                res.Content.Headers.ContentDisposition?.FileNameStar ??
                res.Content.Headers.ContentDisposition?.FileName ??
                $"signnow_{documentId}.pdf";

            var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/pdf";
            var stream = await res.Content.ReadAsStreamAsync(ct);

            // Return as a file so the browser downloads it
            return File(stream, contentType, fileName);
        }

        // reuse your existing helper from other controllers if you prefer
        private bool TryGetBearer(out string token, out IActionResult error)
        {
            token = null!;
            error = null!;
            var auth = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                error = Problem("Missing or invalid Authorization header.", statusCode: 401);
                return false;
            }
            token = auth.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                error = Problem("Invalid or missing access token.", statusCode: 401);
                return false;
            }
            return true;
        }
    }
}
