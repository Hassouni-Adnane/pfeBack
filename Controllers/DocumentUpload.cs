using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private const string SignNowBaseV1 = "https://api.signnow.com";
        private const string SignNowBaseV2 = "https://api.signnow.com/v2";

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _http;
        private readonly ILogger<DocumentController> _log;

        public DocumentController(IHttpClientFactory httpClientFactory, ILogger<DocumentController> log)
        {
            _http = httpClientFactory.CreateClient();
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _log = log;
        }

        /// <summary>
        /// Uploads to SignNow, adds a signature field, then creates an embedded-sending session.
        /// Returns: { document_id, embedded_sending_url }.
        /// </summary>
        [HttpPost("embed-send")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100_000_000)] // ~100 MB; adjust as needed
        public async Task<IActionResult> UploadAndGenerateEmbeddedSending([FromForm] IFormFile file, CancellationToken ct)
        {
            using var scope = _log.BeginScope("EmbedSend {TraceId}", HttpContext.TraceIdentifier);
            _log.LogInformation("Embed-send called at {Utc}", DateTime.UtcNow);

            // 1) Validate inputs
            if (file is null || file.Length == 0)
                return Problem("No file provided.", statusCode: 400);

            if (!TryGetBearer(out var token, out var badAuth))
                return badAuth;

            // 2) Upload
            string documentId;
            try
            {
                documentId = await UploadToSignNowAsync(file, token, ct);
                _log.LogInformation("Uploaded to SignNow. DocumentId={DocumentId}", documentId);
            }
            catch (HttpRequestException ex)
            {
                _log.LogError(ex, "Upload failed");
                return Problem($"Network error calling SignNow (upload): {ex.Message}", statusCode: 503);
            }
            catch (SignNowApiException ex)
            {
                _log.LogWarning("Upload rejected: {Body}", ex.Body);
                return StatusCode(ex.StatusCode, ex.Body);
            }

            // 3) Add field (page_number: 0-based; change to 1 if your account expects 1-based)
            try
            {
                var fieldPayload = new
                {
                    fields = new[]
                    {
                        new
                        {
                            type = "signature",
                            x = 100, y = 150, width = 150, height = 40,
                            page_number = 0,      // ← if needed, set to 1
                            role = "Signer 1",
                            label = "signature_field",
                            required = true
                        }
                    }
                };

                var req = new HttpRequestMessage(HttpMethod.Put, $"{SignNowBaseV1}/document/{documentId}")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(fieldPayload, JsonSettings), Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var res = await _http.SendAsync(req, ct);
                var body = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode)
                    throw new SignNowApiException((int)res.StatusCode, body);

                _log.LogInformation("Signature field added to {DocumentId}", documentId);
            }
            catch (HttpRequestException ex)
            {
                _log.LogError(ex, "Assign fields failed");
                return Problem($"Network error calling SignNow (assign fields): {ex.Message}", statusCode: 503);
            }
            catch (SignNowApiException ex)
            {
                _log.LogError("Field assignment failed: {Body}", ex.Body);
                return StatusCode(ex.StatusCode, ex.Body);
            }

            // 4) Embedded sending
            string embeddedUrl;
            try
            {
                var payload = new
                {
                    type = "document",
                    redirect_uri = "https://www.signnow.com",
                    link_expiration = 45
                };

                var req = new HttpRequestMessage(HttpMethod.Post, $"{SignNowBaseV2}/documents/{documentId}/embedded-sending")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload, JsonSettings), Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                _log.LogInformation("Creating embedded-sending for {DocumentId}", documentId);
                var res = await _http.SendAsync(req, ct);
                var body = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode)
                    throw new SignNowApiException((int)res.StatusCode, body);

                dynamic parsed = JsonConvert.DeserializeObject(body);
                embeddedUrl = parsed?.data?.url;
                if (string.IsNullOrWhiteSpace(embeddedUrl))
                    return Problem("Embedded-sending response did not contain a URL.", statusCode: 502);
            }
            catch (HttpRequestException ex)
            {
                _log.LogError(ex, "Embedded sending failed");
                return Problem($"Network error calling SignNow (embedded sending): {ex.Message}", statusCode: 503);
            }
            catch (SignNowApiException ex)
            {
                _log.LogError("Embedded sending rejected: {Body}", ex.Body);
                return StatusCode(ex.StatusCode, ex.Body);
            }

            // 5) Success
            return Ok(new
            {
                document_id = documentId,
                embedded_sending_url = embeddedUrl
            });
        }

        /// <summary>List your SignNow documents using the bearer token sent by the client.</summary>
        [HttpGet("list-documents")]
        public async Task<IActionResult> ListDocuments(CancellationToken ct)
        {
            if (!TryGetBearer(out var token, out var badAuth))
                return badAuth;

            var req = new HttpRequestMessage(HttpMethod.Get, $"{SignNowBaseV1}/document");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return res.IsSuccessStatusCode
                ? Ok(JsonConvert.DeserializeObject(body))
                : StatusCode((int)res.StatusCode, body);
        }

        // ---------- helpers ----------

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

        private async Task<string> UploadToSignNowAsync(IFormFile file, string token, CancellationToken ct)
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var part = new StreamContent(stream);
            part.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
            );
            content.Add(part, "file", file.FileName);

            var req = new HttpRequestMessage(HttpMethod.Post, $"{SignNowBaseV1}/document") { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new SignNowApiException((int)res.StatusCode, body);

            dynamic parsed = JsonConvert.DeserializeObject(body);
            var id = (string?)parsed?.id;
            if (string.IsNullOrWhiteSpace(id))
                throw new SignNowApiException(502, "Upload succeeded but no document id returned by SignNow.");

            return id!;
        }

        private sealed class SignNowApiException : Exception
        {
            public int StatusCode { get; }
            public string Body { get; }
            public SignNowApiException(int statusCode, string body) : base($"SignNow returned {statusCode}")
            {
                StatusCode = statusCode;
                Body = body ?? string.Empty;
            }
        }
    }
}
