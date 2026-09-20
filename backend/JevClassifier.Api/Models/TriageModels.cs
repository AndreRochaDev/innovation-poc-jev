namespace JevClassifier.Api.Models;

public record ChannelDefinition(string Key, string Label_pt);
public record DepartmentDefinition(string Key, string Label_pt, string Description);
public record PriorityDefinition(string Key, string Label_pt, string Description);

public class JevOptions
{
    public string BaseUrl { get; set; } = "https://opencode.ai/zen/v1/";
    public string Model { get; set; } = "jev-1.13-free";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxStateChars { get; set; } = 50000;
    public int MinTextChars { get; set; } = 20;
}

public class TriageOptions
{
    public List<ChannelDefinition> Channels { get; set; } = new();
    public List<DepartmentDefinition> Departments { get; set; } = new();
    public List<PriorityDefinition> Priorities { get; set; } = new();
}

public record TriageResultDto(
    string Departamento,
    string DepartamentoLabel,
    double Confianca,
    Dictionary<string, double> Probabilidades,
    bool Urgente,
    double ProbUrgente,
    string Frustracao,
    Dictionary<string, double>? FrustracaoProbs,
    string Prioridade,
    string SlaSugerido,
    string EquipaSugerida,
    bool ConfiancaBaixa,
    string Modelo,
    string Canal);

public record TaxonomyResponse(
    List<ChannelDefinition> Channels,
    List<DepartmentDefinition> Departments,
    List<PriorityDefinition> Priorities,
    string Model);

// --- JEV wire format (SystemOne) ---
public class JevQuestion
{
    public string Type { get; set; } = "choice"; // noul | choice | score
    public string Instructions { get; set; } = "";
    public object? Criteria { get; set; }
}

public class JevRequest
{
    public string Model { get; set; } = "";
    public string State { get; set; } = "";
    public Dictionary<string, JevQuestion> Questions { get; set; } = new();
}

public class JevAnswer
{
    public string? Value { get; set; }
    public double? Probability { get; set; }
    public Dictionary<string, double>? Probabilities { get; set; }
    public Dictionary<string, double>? Scores { get; set; }
}

public class JevResponse
{
    public Dictionary<string, JevAnswer>? Answers { get; set; }
    public Dictionary<string, JevAnswer>? Results { get; set; }
}
