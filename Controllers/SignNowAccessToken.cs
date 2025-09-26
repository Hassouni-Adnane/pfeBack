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
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string BasicToken { get; set; } = string.Empty; // 👈 added
        }

        [HttpPost("auth")]
        public async Task<IActionResult> AuthenticateWithPassword([FromBody] LoginRequest login)
        {
            // (Optional) Remove these Console.WriteLine in production
            Console.WriteLine($"Username: {login.Username}");
            Console.WriteLine($"Password: {login.Password}");
            Console.WriteLine($"BasicToken: {login.BasicToken}");

            if (string.IsNullOrEmpty(login.BasicToken))
                return BadRequest("Basic token is required.");

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

            var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = content
            };
            // 👇 Use token from frontend
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", login.BasicToken);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, $"Erreur lors de l'appel à SignNow : {ex.Message}");
            }
            

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, responseString);

            return Ok(responseString);
        }
    }
}
