using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SignNowBackend.Services;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class OrchestratorController : BaseApiController
    {
        private readonly SignNowService _sn;
        private readonly NodeReporter _node;

        public OrchestratorController(SignNowService sn, NodeReporter node)
        {
            _sn = sn; _node = node;
        }

        [HttpPost("embed-send")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> EmbedSend([FromForm] IFormFile file, [FromForm] string workflow,
            [FromForm] string? uploaderUserId, CancellationToken ct)
        {
            if (!TryGetBearer(out var token, out var err)) return err;
            if (file is null || file.Length == 0) return Problem("No file provided.", statusCode: 400);

            var wf = (workflow ?? "").Trim().ToLowerInvariant();
            if (wf != "parallel" && wf != "sequential")
                return Problem("workflow must be 'parallel' or 'sequential'.", statusCode: 400);

            var id  = await _sn.UploadAsync(file, token, ct);
            await _sn.AddSignatureFieldAsync(id, token, ct);
            var url = await _sn.CreateEmbeddedSendingAsync(id, token, ct);

            await _node.ReportAsync(new {
                signNowDocumentId = id,
                workflow = wf,
                uploadedAt = DateTimeOffset.UtcNow,
                originalName = file.FileName,
                contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                sizeBytes = file.Length,
                embeddedSendingUrl = url,
                uploaderUserId = string.IsNullOrWhiteSpace(uploaderUserId) ? null : uploaderUserId,
                meta = new { traceId = HttpContext.TraceIdentifier }
            }, ct);

            return Ok(new { document_id = id, embedded_sending_url = url });
        }
    }
}
