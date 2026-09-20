using JevClassifier.Api.Models;
using JevClassifier.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JevClassifier.Api.Controllers;

[ApiController]
[Route("api")]
public class TriageController : ControllerBase
{
    private readonly TriageService _triage;
    private readonly TriageOptions _triageOpts;
    private readonly JevOptions _jevOpts;

    public TriageController(TriageService triage, IOptions<TriageOptions> triageOpts, IOptions<JevOptions> jevOpts)
    {
        _triage = triage;
        _triageOpts = triageOpts.Value;
        _jevOpts = jevOpts.Value;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", model = _jevOpts.Model });

    [HttpGet("taxonomy")]
    public ActionResult<TaxonomyResponse> Taxonomy() =>
        Ok(new TaxonomyResponse(_triageOpts.Channels, _triageOpts.Departments, _triageOpts.Priorities, _jevOpts.Model));

    public class TriageJsonBody
    {
        public string? Channel { get; set; }
        public string? Text { get; set; }
    }

    [HttpPost("triage")]
    [Consumes("application/json")]
    public async Task<ActionResult<TriageResultDto>> TriageJson([FromBody] TriageJsonBody body, CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Text))
            return BadRequest(new { error = "Campo 'text' é obrigatório." });
        return await DoTriage(body.Channel ?? "portal", body.Text, ct);
    }

    [HttpPost("classify")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TriageResultDto>> ClassifyForm(
        [FromForm] string channel,
        [FromForm] string? text,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        string content = text ?? "";
        if (file is not null && file.Length > 0)
        {
            try { content = await DocumentTextExtractor.ExtractAsync(file, ct); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        }
        return await DoTriage(channel, content, ct);
    }

    private async Task<ActionResult<TriageResultDto>> DoTriage(string channel, string text, CancellationToken ct)
    {
        if (!_triageOpts.Channels.Any(c => c.Key == channel))
            return BadRequest(new { error = $"Canal inválido. Use: {string.Join(", ", _triageOpts.Channels.Select(c => c.Key))}" });
        try
        {
            var result = await _triage.TriageAsync(channel, text, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
    }
}
