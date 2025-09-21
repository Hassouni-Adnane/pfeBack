using Microsoft.AspNetCore.Mvc;
using SignNowBackend.Services;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId}/embedded-sending")]
    public class EmbeddedController : BaseApiController
    {
        private readonly SignNowService _sn;
        private readonly NodeReporter _node;

        public EmbeddedController(SignNowService sn, NodeReporter node)
        {
            _sn = sn; _node = node;
        }

        // POST /api/documents/{id}/embedded-sending?workflow=parallel&uploaderUserId=42
        [HttpPost]
        public async Task<IActionResult> Create(string documentId, [FromQuery] string workflow,
            [FromQuery] string? uploaderUserId, CancellationToken ct)
        {
            if (!TryGetBearer(out var token, out var err)) return err;

            var wf = (workflow ?? "").Trim().ToLowerInvariant();
            if (wf != "parallel" && wf != "sequential")
                return Problem("workflow must be 'parallel' or 'sequential'.", statusCode: 400);

            var url = await _sn.CreateEmbeddedSendingAsync(documentId, token, ct);

            // Optional: notify Node (no file metadata here since no file in this step)
            await _node.ReportAsync(new
            {
                signNowDocumentId = documentId,
                workflow = wf,
                uploadedAt = DateTimeOffset.UtcNow,
                embeddedSendingUrl = url,
                uploaderUserId
            }, ct);

            return Ok(new { document_id = documentId, embedded_sending_url = url });
        }
    }
}
