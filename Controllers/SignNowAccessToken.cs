using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SignNowController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public SignNowController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        public class LoginRequest
        {
            // Initialize to empty string so the compiler knows they’re never null
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("auth")]
        public async Task<IActionResult> AuthenticateWithPassword([FromBody] LoginRequest login)
        {
            // (Optional) Remove these Console.WriteLine in production
            Console.WriteLine($"Username: {login.Username}");
            Console.WriteLine($"Password: {login.Password}");

            var authUrl = "https://api.signnow.com/oauth2/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", login.Username),
                new KeyValuePair<string, string>("password", login.Password),
                new KeyValuePair<string, string>("scope", "*")
            });

            Console.WriteLine("Contenu de la requête envoyée à SignNow:");
            Console.WriteLine(await content.ReadAsStringAsync());

            var basicToken = Environment.GetEnvironmentVariable("SIGNNOW_BASIC_TOKEN");
            if (string.IsNullOrEmpty(basicToken))
                return StatusCode(500, "Le Basic token n'est pas configuré.");

            var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                // Network failure
                return StatusCode(503, $"Erreur lors de l'appel à SignNow : {ex.Message}");
            }

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, responseString);

            return Ok(responseString);
        }
    }
}
