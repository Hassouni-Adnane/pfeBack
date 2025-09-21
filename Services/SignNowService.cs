using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SignNowBackend.Services
{
    public sealed class SignNowService
    {
        private const string V1 = "https://api.signnow.com";
        private const string V2 = "https://api.signnow.com/v2";

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _http;
        private readonly ILogger<SignNowService> _log;

        public SignNowService(IHttpClientFactory factory, ILogger<SignNowService> log)
        {
            _http = factory.CreateClient();
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _log = log;
        }

        public async Task<string> UploadAsync(IFormFile file, string bearer, CancellationToken ct)
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var part = new StreamContent(stream);
            part.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
            );
            content.Add(part, "file", file.FileName);

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{V1}/document") { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new SignNowApiException((int)res.StatusCode, body);

            dynamic parsed = JsonConvert.DeserializeObject(body);
            var id = (string?)parsed?.id;
            if (string.IsNullOrWhiteSpace(id)) throw new SignNowApiException(502, "Upload succeeded but no document id returned.");
            return id!;
        }

        public async Task AddSignatureFieldAsync(string documentId, string bearer, CancellationToken ct,
            int pageNumber = 0, int x = 100, int y = 150, int width = 150, int height = 40,
            string role = "Signer 1", string label = "signature_field")
        {
            var payload = new
            {
                fields = new[]
                {
                    new { type = "signature", x, y, width, height, page_number = pageNumber, role, label, required = true }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Put, $"{V1}/document/{documentId}")
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload, JsonSettings), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new SignNowApiException((int)res.StatusCode, body);
        }

        public async Task<string> CreateEmbeddedSendingAsync(string documentId, string bearer, CancellationToken ct,
            string redirect = "https://www.signnow.com", int linkExpiration = 45)
        {
            var payload = new { type = "document", redirect_uri = redirect, link_expiration = linkExpiration };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{V2}/documents/{documentId}/embedded-sending")
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload, JsonSettings), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new SignNowApiException((int)res.StatusCode, body);

            dynamic parsed = JsonConvert.DeserializeObject(body);
            var url = (string?)parsed?.data?.url;
            if (string.IsNullOrWhiteSpace(url)) throw new SignNowApiException(502, "Embedded-sending response did not contain a URL.");
            return url!;
        }

        public sealed class SignNowApiException : Exception
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
