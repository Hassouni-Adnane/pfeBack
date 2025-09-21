using Microsoft.AspNetCore.Mvc;

namespace SignNowBackend.Controllers
{
    public abstract class BaseApiController : ControllerBase
    {
        protected bool TryGetBearer(out string token, out IActionResult error)
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
