using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/signing-links")]
    public sealed class SigningLinksController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _signNowBase;

        public SigningLinksController(IHttpClientFactory factory)
        {
            _http = factory.CreateClient();
            _signNowBase = Environment.GetEnvironmentVariable("SIGNNOW_API_BASE")
                           ?? "https://api.signnow.com";
        }

        public sealed class CreateSigningLinkRequest
        {
            public string token { get; set; } = string.Empty;   // SignNow access_token (Bearer)
            public string DocumentId { get; set; } = string.Empty;
            public string? RedirectUri { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSigningLinkRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.token))
                return Problem("access_token is required.", statusCode: 400);
            if (string.IsNullOrWhiteSpace(req.DocumentId))
                return Problem("document_id is required.", statusCode: 400);

            var url = $"{_signNowBase.TrimEnd('/')}/link";

            var payload = new
            {
                document_id = req.DocumentId,
                redirect_uri = string.IsNullOrWhiteSpace(req.RedirectUri) ? null : req.RedirectUri
            };

            using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };
            // ✅ Use Bearer scheme
            httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.token);

            HttpResponseMessage snRes;
            try
            {
                snRes = await _http.SendAsync(httpReq, ct);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, $"Network error calling SignNow: {ex.Message}");
            }

            var body = await snRes.Content.ReadAsStringAsync(ct);
            if (!snRes.IsSuccessStatusCode) return StatusCode((int)snRes.StatusCode, body);

            return Content(body, "application/json", Encoding.UTF8);
        }
    }
}
