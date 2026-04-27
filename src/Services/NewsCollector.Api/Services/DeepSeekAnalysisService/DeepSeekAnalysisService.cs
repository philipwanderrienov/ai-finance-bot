using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService;

public sealed class DeepSeekAnalysisService : IDeepSeekAnalysisService
{
    private const string DefaultModelName = "deepseek-chat";
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly NewsDbContext _dbContext;
    private readonly DeepSeekOptions _options;

    public DeepSeekAnalysisService(NewsDbContext dbContext, IOptions<DeepSeekOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<DeepSeekAnalysisResult> AnalyzeAsync(DeepSeekAnalysisRequest request, CancellationToken cancellationToken)
    {
        var articles = await _dbContext.NewsArticles
            .AsNoTracking()
            .Where(x => x.Category == request.Category && x.SourceName == "Polymarket")
            .OrderByDescending(x => x.PublishedAt)
            .Take(Math.Clamp(request.LookbackCount, 1, 50))
            .ToListAsync(cancellationToken);

        if (articles.Count == 0)
        {
            throw new InvalidOperationException($"No Polymarket articles found for category {request.Category}.");
        }

        var prompt = BuildPrompt(request, articles);
        var analysis = await RequestDeepSeekAsync(prompt, cancellationToken);

        var entity = new DeepSeekAnalysisEntity
        {
            Id = Guid.NewGuid(),
            Category = request.Category,
            Symbol = request.Symbol,
            ModelName = ResolveModelName(),
            Summary = analysis.Summary,
            Confidence = analysis.Confidence,
            Verdict = analysis.Verdict,
            Reason = analysis.Reason,
            KeyPoints = analysis.KeyPoints.ToList(),
            RiskFactors = analysis.RiskFactors.ToList(),
            SourceUrls = articles.Select(x => x.Url).Distinct().ToList(),
            GeneratedAt = DateTimeOffset.UtcNow,
            Prompt = prompt,
            RawResponse = analysis.RawResponse
        };

        _dbContext.DeepSeekAnalyses.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<IReadOnlyList<DeepSeekAnalysisResult>> GetLatestAsync(NewsCategory category, string symbol, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 50);

        var results = await _dbContext.DeepSeekAnalyses
            .AsNoTracking()
            .Where(x => x.Category == category && x.Symbol == symbol)
            .OrderByDescending(x => x.GeneratedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return results.Select(Map).ToArray();
    }

    private async Task<DeepSeekAnalysisPayload> RequestDeepSeekAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return BuildOfflinePayload(prompt);
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api.deepseek.com" : _options.BaseUrl.TrimEnd('/');
        var requestBody = new
        {
            model = ResolveModelName(),
            messages = new[]
            {
                new { role = "system", content = "You analyze market news and return strict JSON." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = ExtractAssistantContent(responseText);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("DeepSeek returned an empty response.");
        }

        var payload = ParsePayload(content);
        return payload with { RawResponse = responseText };
    }

    private static DeepSeekAnalysisPayload BuildOfflinePayload(string prompt)
    {
        return new DeepSeekAnalysisPayload(
            Summary: "DeepSeek API key is missing, so this analysis was generated from local rules.",
            Confidence: 0.55m,
            Verdict: "neutral",
            Reason: "Offline fallback because DeepSeek API key is not configured.",
            KeyPoints: new[] { prompt.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "No prompt available" },
            RiskFactors: new[] { "DeepSeek API key not configured" },
            RawResponse: null);
    }

    private static string ExtractAssistantContent(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);

        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return content.GetString() ?? string.Empty;
    }

    private static DeepSeekAnalysisPayload ParsePayload(string content)
    {
        var normalized = content.Trim().Trim('`');
        if (normalized.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..].Trim();
        }

        try
        {
            using var document = JsonDocument.Parse(normalized);
            var root = document.RootElement;

            return new DeepSeekAnalysisPayload(
                Summary: GetString(root, "summary"),
                Confidence: GetDecimal(root, "confidence"),
                Verdict: GetString(root, "verdict"),
                Reason: GetString(root, "reason"),
                KeyPoints: GetStringArray(root, "keyPoints"),
                RiskFactors: GetStringArray(root, "riskFactors"),
                RawResponse: content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("DeepSeek response was not valid JSON.", ex);
        }
    }

    private static string GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static decimal GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0.5m;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0.5m
        };
    }

    private static IReadOnlyCollection<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private string BuildPrompt(DeepSeekAnalysisRequest request, IReadOnlyList<NewsArticleEntity> articles)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Analyze Polymarket news for {request.Category} ({request.Symbol}).");
        builder.AppendLine("Return valid JSON with keys: summary, confidence, verdict, reason, keyPoints, riskFactors.");
        builder.AppendLine("Use only the provided news items as context.");
        builder.AppendLine();

        foreach (var article in articles)
        {
            builder.AppendLine($"- {article.PublishedAt:O} | {article.Title} | {article.Summary} | {article.Url}");
        }

        return builder.ToString();
    }

    private string ResolveModelName()
        => string.IsNullOrWhiteSpace(_options.BaseUrl) ? DefaultModelName : DefaultModelName;

    private static DeepSeekAnalysisResult Map(DeepSeekAnalysisEntity entity)
        => new(
            entity.Id,
            entity.Category,
            entity.Symbol,
            entity.ModelName,
            entity.Summary,
            entity.Confidence,
            entity.Verdict,
            entity.Reason,
            entity.KeyPoints,
            entity.RiskFactors,
            entity.SourceUrls,
            entity.GeneratedAt);

    private sealed record DeepSeekAnalysisPayload(
        string Summary,
        decimal Confidence,
        string Verdict,
        string Reason,
        IReadOnlyCollection<string> KeyPoints,
        IReadOnlyCollection<string> RiskFactors,
        string? RawResponse);
}
