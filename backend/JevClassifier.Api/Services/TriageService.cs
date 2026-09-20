using JevClassifier.Api.Models;
using Microsoft.Extensions.Options;

namespace JevClassifier.Api.Services;

public class TriageService
{
    private readonly IJevClient _jev;
    private readonly JevOptions _jevOpts;
    private readonly TriageOptions _triage;

    public TriageService(IJevClient jev, IOptions<JevOptions> jevOpts, IOptions<TriageOptions> triage)
    {
        _jev = jev;
        _jevOpts = jevOpts.Value;
        _triage = triage.Value;
    }

    public async Task<TriageResultDto> TriageAsync(string channel, string rawText, CancellationToken ct)
    {
        var text = DocumentTextExtractor.Normalize(rawText);
        if (text.Length < _jevOpts.MinTextChars)
            throw new InvalidOperationException($"Texto insuficiente ({text.Length} chars, mínimo {_jevOpts.MinTextChars}). Cole mais conteúdo.");

        if (text.Length > _jevOpts.MaxStateChars)
            text = text[.._jevOpts.MaxStateChars];

        var channelLabel = _triage.Channels.FirstOrDefault(c => c.Key == channel)?.Label_pt ?? channel;
        var state = $"[Canal: {channelLabel}]\n{text}";

        var deptCriteria = _triage.Departments.ToDictionary(d => d.Key, d => $"{d.Label_pt}: {d.Description}");
        var questions = new Dictionary<string, JevQuestion>
        {
            ["departamento"] = new JevQuestion
            {
                Type = "choice",
                Instructions = "Classifica esta reclamação municipal portuguesa num departamento. Responde em português europeu.",
                Criteria = deptCriteria
            },
            ["urgente"] = new JevQuestion
            {
                Type = "noul",
                Instructions = "Requer intervenção urgente? Perigo, saúde pública, corte de serviço essencial, dano ativo."
            },
            ["frustracao"] = new JevQuestion
            {
                Type = "score",
                Instructions = "Qual o nível de frustração do munícipe?",
                Criteria = new[] { "Calmo", "Frustrado", "Muito revoltado" }
            },
            ["prioridade"] = new JevQuestion
            {
                Type = "choice",
                Instructions = "Sugere prioridade operacional.",
                Criteria = new Dictionary<string, string>
                {
                    ["P1"] = "Urgente 24h: perigo, saúde, corte essencial",
                    ["P2"] = "Normal 5 dias úteis: falha de serviço sem perigo imediato",
                    ["P3"] = "Baixa 20 dias: sugestão, melhoria, incómodo menor"
                }
            }
        };

        var jev = await _jev.EvaluateAsync(state, questions, ct);
        var answers = jev.Answers ?? jev.Results ?? new Dictionary<string, JevAnswer>();

        string Str(string k) => answers.TryGetValue(k, out var a) ? (a.Value ?? "") : "";
        double Prob(string k) => answers.TryGetValue(k, out var a) ? (a.Probability ?? 0) : 0;

        var dept = Str("departamento");
        if (string.IsNullOrWhiteSpace(dept)) dept = "outro";
        var deptProbs = answers.TryGetValue("departamento", out var dA) ? (dA.Probabilities ?? new()) : new();

        var urgVal = Str("urgente").ToLowerInvariant();
        var urgProb = Prob("urgente");
        bool urgente = urgVal is "yes" or "true" or "sim" || urgProb >= 0.5;

        var frust = Str("frustracao");
        if (string.IsNullOrWhiteSpace(frust)) frust = "Frustrado";
        var frustProbs = answers.TryGetValue("frustracao", out var fA) ? (fA.Scores ?? fA.Probabilities) : null;

        var prio = Str("prioridade");
        if (prio is not ("P1" or "P2" or "P3"))
            prio = urgente ? "P1" : "P2"; // fallback rule

        var (sla, equipa) = prio switch
        {
            "P1" => ("24 horas", TeamFor(dept)),
            "P2" => ("5 dias úteis", TeamFor(dept)),
            _ => ("20 dias", TeamFor(dept))
        };

        var topProb = deptProbs.Count > 0 ? deptProbs.Values.Max() : Prob("departamento");
        var deptLabel = _triage.Departments.FirstOrDefault(d => d.Key == dept)?.Label_pt ?? dept;

        return new TriageResultDto(
            dept, deptLabel, Math.Round(topProb, 3), deptProbs,
            urgente, Math.Round(urgProb, 3), frust, frustProbs,
            prio, sla, equipa,
            topProb < 0.6, _jevOpts.Model, channel);
    }

    private string TeamFor(string dept) =>
        _triage.Departments.FirstOrDefault(d => d.Key == dept)?.Label_pt ?? dept;
}
