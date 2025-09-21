using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SignNowBackend.Services;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : BaseApiController
    {
        private readonly SignNowService _sn;
        private readonly ILogger<UploadController> _log;

        public UploadController(SignNowService sn, ILogger<UploadController> log)
        {
            _sn = sn; _log = log;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
        {
            if (!TryGetBearer(out var token, out var err)) return err;
            if (file is null || file.Length == 0) return Problem("No file provided.", statusCode: 400);

            var id = await _sn.UploadAsync(file, token, ct);
            _log.LogInformation("Uploaded to SignNow: {Id}", id);
            return Ok(new { document_id = id });
        }
    }
}
