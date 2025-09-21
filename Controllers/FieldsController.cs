using Microsoft.AspNetCore.Mvc;
using SignNowBackend.Services;

namespace SignNowBackend.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId}/fields")]
    public class FieldsController : BaseApiController
    {
        private readonly SignNowService _sn;

        public FieldsController(SignNowService sn) { _sn = sn; }

        // PUT /api/documents/{id}/fields/signature
        [HttpPut("signature")]
        public async Task<IActionResult> AddSignatureField(string documentId, CancellationToken ct,
            [FromQuery] int pageNumber = 0, [FromQuery] int x = 100, [FromQuery] int y = 150,
            [FromQuery] int width = 150, [FromQuery] int height = 40,
            [FromQuery] string role = "Signer 1", [FromQuery] string label = "signature_field")
        {
            if (!TryGetBearer(out var token, out var err)) return err;
            await _sn.AddSignatureFieldAsync(documentId, token, ct, pageNumber, x, y, width, height, role, label);
            return Ok(new { ok = true });
        }
    }
}
