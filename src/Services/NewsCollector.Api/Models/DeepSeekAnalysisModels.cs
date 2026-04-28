namespace NewsCollector.Api.Models;

public sealed record DeepSeekAnalysisRequest(
    NewsCategory Category,
    string Symbol,
    int LookbackCount = 25);

public sealed record DeepSeekAnalysisResult(
    Guid Id,
    NewsCategory Category,
    string Symbol,
    string ModelName,
    string Summary,
    decimal Confidence,
    string Verdict,
    string Reason,
    IReadOnlyCollection<string> KeyPoints,
    IReadOnlyCollection<string> RiskFactors,
    IReadOnlyCollection<string> SourceUrls,
    DateTimeOffset GeneratedAt);

public sealed class DeepSeekAnalysisEntity
{
    public Guid Id { get; set; }
    public NewsCategory Category { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = [];
    public List<string> RiskFactors { get; set; } = [];
    public List<string> SourceUrls { get; set; } = [];
    public DateTimeOffset GeneratedAt { get; set; }
    public string? Prompt { get; set; }
    public string? RawResponse { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public int InputArticleCount { get; set; }
}
