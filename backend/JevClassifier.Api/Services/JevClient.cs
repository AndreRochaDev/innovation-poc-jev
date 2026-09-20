using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JevClassifier.Api.Models;

namespace JevClassifier.Api.Services;

public interface IJevClient
{
    Task<JevResponse> EvaluateAsync(string state, Dictionary<string, JevQuestion> questions, CancellationToken ct);
}

public class JevClient : IJevClient
{
    private readonly HttpClient _http;
    private readonly JevOptions _options;
    private readonly ILogger<JevClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public JevClient(HttpClient http, Microsoft.Extensions.Options.IOptions<JevOptions> options, ILogger<JevClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JevResponse> EvaluateAsync(string state, Dictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENCODE_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENCODE_API_KEY não configurada no servidor.");

        var payload = new JevRequest { Model = _options.Model, State = state, Questions = questions };
        var json = JsonSerializer.Serialize(payload, JsonOpts);

        using var req = new HttpRequestMessage(HttpMethod.Post, "systemone");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("JEV request model={Model} stateChars={N} questions={Q}",
            _options.Model, state.Length, string.Join(",", questions.Keys));

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("JEV error {Status}: {Body}", (int)res.StatusCode, body[..Math.Min(body.Length, 1000)]);
            throw new HttpRequestException($"JEV retornou {(int)res.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
        }

        var parsed = JsonSerializer.Deserialize<JevResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Resposta JEV vazia.");
        return parsed;
    }
}
