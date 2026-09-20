using JevClassifier.Api.Models;
using Microsoft.Extensions.Options;

namespace JevClassifier.Api.Services;

public class TriageService
{
    private readonly IJevClient _jev;
    private readonly JevOptions _jevOpts;
    private readonly TriageOptions _triage;
    private readonly ILogger<TriageService> _logger;

    // Níveis do Score "frustracao" (ordem = índice 0..2, ver docs TypeSafe).
    private static readonly string[] FrustracaoLevels = ["Calmo", "Frustrado", "Muito revoltado"];

    public TriageService(IJevClient jev, IOptions<JevOptions> jevOpts, IOptions<TriageOptions> triage, ILogger<TriageService> logger)
    {
        _jev = jev;
        _jevOpts = jevOpts.Value;
        _triage = triage.Value;
        _logger = logger;
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
                Instructions = "Esta reclamação requer intervenção urgente por parte do município?",
                Criteria = new Dictionary<string, string>
                {
                    ["true"] = "Perigo para pessoas, risco de saúde pública, corte de serviço essencial, dano ativo a decorrer",
                    ["false"] = "Incómodo, sugestão de melhoria ou falha sem perigo nem corte essencial"
                }
            },
            ["frustracao"] = new JevQuestion
            {
                Type = "score",
                Instructions = "Qual o nível de frustração do munícipe?",
                Criteria = new[]
                {
                    "Calmo, apenas a expor os factos",
                    "Frustrado mas cordial, já reportou antes ou mostra impaciência",
                    "Muito revoltado, linguagem forte ou a exigir resolução imediata"
                }
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

        // --- Choice: campo nativo é "choice" (com "confidence" e "probabilities") ---
        string ChoiceOf(string k, string fallback)
        {
            if (!answers.TryGetValue(k, out var a) || a is null) return fallback;
            if (!string.IsNullOrWhiteSpace(a.Choice)) return a.Choice!;
            if (!string.IsNullOrWhiteSpace(a.Value)) return a.Value!;
            return fallback;
        }

        Dictionary<string, double> ProbsOf(string k)
        {
            if (!answers.TryGetValue(k, out var a) || a is null) return new();
            // Score usa chaves "0","1",... ; Choice usa nomes das opções.
            return a.Probabilities ?? a.Scores ?? new();
        }

        double ConfidenceOf(string k, Dictionary<string, double> probs)
        {
            if (probs.Count > 0) return probs.Values.Max();
            if (!answers.TryGetValue(k, out var a) || a is null) return 0;
            return a.Confidence ?? a.Probability ?? a.Noul ?? a.Score ?? 0;
        }

        var dept = ChoiceOf("departamento", "outro");
        var deptProbs = ProbsOf("departamento");
        var deptConf = ConfidenceOf("departamento", deptProbs);

        // --- Noul: número único "noul" 0-1 (probabilidade de "sim") ---
        double noul = 0;
        if (answers.TryGetValue("urgente", out var u) && u is not null)
            noul = u.Noul ?? u.Probability ?? 0;
        bool urgente = noul >= 0.5;

        // --- Score: posição "score" sobre os níveis 0..N; arredonda p/ nível ---
        double score = 1;
        if (answers.TryGetValue("frustracao", out var f) && f is not null && f.Score.HasValue)
            score = f.Score.Value;
        int levelIdx = Math.Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero), 0, FrustracaoLevels.Length - 1);
        var frust = FrustracaoLevels[levelIdx];
        Dictionary<string, double>? frustProbs = null;
        var frustRaw = ProbsOf("frustracao");
        if (frustRaw.Count > 0)
        {
            frustProbs = new Dictionary<string, double>();
            for (int i = 0; i < FrustracaoLevels.Length; i++)
                if (frustRaw.TryGetValue(i.ToString(), out var p))
                    frustProbs[FrustracaoLevels[i]] = Math.Round(p, 3);
        }

        _logger.LogInformation(
            "Triage canal={Canal} dept={Dept} (conf {DConf}) urgente={U} (noul {N}) frustracao={F} (score {S})",
            channel, dept, Math.Round(deptConf, 3), urgente, Math.Round(noul, 3), frust, Math.Round(score, 2));

        var prio = ChoiceOf("prioridade", urgente ? "P1" : "P2");
        if (prio is not ("P1" or "P2" or "P3"))
            prio = urgente ? "P1" : "P2"; // fallback rule

        var (sla, equipa) = prio switch
        {
            "P1" => ("24 horas", TeamFor(dept)),
            "P2" => ("5 dias úteis", TeamFor(dept)),
            _ => ("20 dias", TeamFor(dept))
        };

        var deptLabel = _triage.Departments.FirstOrDefault(d => d.Key == dept)?.Label_pt ?? dept;

        return new TriageResultDto(
            dept, deptLabel, Math.Round(deptConf, 3), deptProbs,
            urgente, Math.Round(noul, 3), frust, frustProbs,
            prio, sla, equipa,
            deptConf < 0.6, _jevOpts.Model, channel);
    }

    private string TeamFor(string dept) =>
        _triage.Departments.FirstOrDefault(d => d.Key == dept)?.Label_pt ?? dept;
}
