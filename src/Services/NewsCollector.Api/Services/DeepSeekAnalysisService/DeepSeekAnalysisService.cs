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
    private const string DefaultModelName = "deepseek-reasoner";
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly NewsDbContext _dbContext;
    private readonly DeepSeekOptions _options;
    private readonly IAnalysisCandidateBuilder _candidateBuilder;
    private readonly IAnalysisFingerprintService _fingerprintService;

    public DeepSeekAnalysisService(
        NewsDbContext dbContext,
        IOptions<DeepSeekOptions> options,
        IAnalysisCandidateBuilder candidateBuilder,
        IAnalysisFingerprintService fingerprintService)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _candidateBuilder = candidateBuilder;
        _fingerprintService = fingerprintService;
    }

    public async Task<DeepSeekAnalysisResult> AnalyzeAsync(DeepSeekAnalysisRequest request, CancellationToken cancellationToken)
    {
        var candidates = await _candidateBuilder.BuildAsync(request, cancellationToken);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No articles found for category {request.Category}.");
        }

        var prompt = BuildPrompt(request, candidates);
        var analysis = await RequestDeepSeekAsync(request, candidates, prompt, cancellationToken);

        var marketPrice = NormalizeMarketPrice(request.MarketPrice ?? InferMarketPrice(request.Category));
        var gap = Math.Round(analysis.Confidence - marketPrice, 4, MidpointRounding.AwayFromZero);
        var signal = ResolveDivergenceSignal(analysis.Confidence, marketPrice);

        return new DeepSeekAnalysisResult(
            Guid.NewGuid(),
            request.Category,
            request.Symbol,
            ResolveModelName(),
            analysis.Summary,
            analysis.Confidence,
            analysis.Verdict,
            analysis.Reason,
            analysis.Confidence,
            marketPrice,
            gap,
            signal,
            analysis.KeyPoints.ToArray(),
            analysis.RiskFactors.ToArray(),
            candidates.Select(x => x.Url).Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            DateTimeOffset.UtcNow);
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

    private async Task<DeepSeekAnalysisPayload> RequestDeepSeekAsync(
        DeepSeekAnalysisRequest request,
        IReadOnlyList<AnalysisCandidate> candidates,
        string prompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return BuildSimulatorPayload(request, candidates, prompt);
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

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(requestMessage, cancellationToken);
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

    private static DeepSeekAnalysisPayload BuildSimulatorPayload(
        DeepSeekAnalysisRequest request,
        IReadOnlyList<AnalysisCandidate> candidates,
        string prompt)
    {
        var topCandidates = candidates
            .OrderByDescending(candidate => candidate.SentimentScore ?? 0.5m)
            .ThenByDescending(candidate => candidate.PublishedAt)
            .Take(5)
            .ToArray();

        var averageSentiment = topCandidates.Length == 0
            ? 0.5m
            : topCandidates.Average(candidate => candidate.SentimentScore ?? 0.5m);

        var positiveSignals = topCandidates.Count(candidate =>
            ContainsAny($"{candidate.Title} {candidate.Summary}", "approve", "surge", "bull", "win", "growth", "strong", "rally"));

        var negativeSignals = topCandidates.Count(candidate =>
            ContainsAny($"{candidate.Title} {candidate.Summary}", "risk", "drop", "ban", "delay", "fear", "decline", "lawsuit"));

        var momentum = topCandidates.Length == 0 ? 0m : (positiveSignals - negativeSignals) / (decimal)topCandidates.Length;
        var confidence = Math.Clamp(0.50m + ((averageSentiment - 0.5m) * 0.85m) + (momentum * 0.18m), 0.15m, 0.98m);

        var verdict = confidence switch
        {
            >= 0.68m => "bullish",
            <= 0.42m => "bearish",
            _ => averageSentiment >= 0.52m ? "bullish" : averageSentiment <= 0.48m ? "bearish" : "neutral"
        };

        var summaryTone = verdict switch
        {
            "bullish" => "constructive",
            "bearish" => "cautious",
            _ => "balanced"
        };

        var summary = $"DeepSeek simulator produced a {summaryTone} read on {request.Category} ({request.Symbol}) from {candidates.Count} Polymarket articles.";
        var reason = $"Offline simulator used because DeepSeek API key is not configured. Average sentiment={averageSentiment:0.00}, positiveSignals={positiveSignals}, negativeSignals={negativeSignals}, momentum={momentum:0.00}.";
        var keyPoints = topCandidates
            .Select(candidate => $"{candidate.PublishedAt:yyyy-MM-dd}: {candidate.Title}")
            .Distinct()
            .Take(5)
            .ToArray();

        if (keyPoints.Length == 0)
        {
            keyPoints = new[]
            {
                prompt.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "No prompt available"
            };
        }

        var riskFactors = BuildRiskFactors(request, candidates, averageSentiment, negativeSignals);
        return new DeepSeekAnalysisPayload(summary, confidence, verdict, reason, keyPoints, riskFactors, null);
    }

    private static IReadOnlyCollection<string> BuildRiskFactors(
        DeepSeekAnalysisRequest request,
        IReadOnlyList<AnalysisCandidate> candidates,
        decimal averageSentiment,
        int negativeSignals)
    {
        var riskFactors = new List<string>
        {
            $"No DeepSeek API key configured; using simulator for {request.Category}/{request.Symbol}.",
            $"Average sentiment score is {averageSentiment:0.00}."
        };

        if (negativeSignals > 0)
        {
            riskFactors.Add($"{negativeSignals} negative-signal articles detected in the top sample.");
        }

        if (candidates.Count < request.LookbackCount)
        {
            riskFactors.Add($"Only {candidates.Count} candidates available from requested lookback {request.LookbackCount}.");
        }

        return riskFactors;
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

    private string BuildPrompt(DeepSeekAnalysisRequest request, IReadOnlyList<AnalysisCandidate> candidates)
    {
        var marketPrice = NormalizeMarketPrice(request.MarketPrice ?? InferMarketPrice(request.Category));
        var builder = new StringBuilder();
        builder.AppendLine($"Analyze news for {request.Category} ({request.Symbol}).");
        builder.AppendLine($"Polymarket price reference: {marketPrice:0.00}.");
        builder.AppendLine("Return valid JSON with keys: summary, confidence, verdict, reason, keyPoints, riskFactors.");
        builder.AppendLine("Use only the provided news items as context.");
        builder.AppendLine("Confidence should represent the AI-calculated probability of the event happening.");
        builder.AppendLine("Signal should be based on divergence between AI confidence and market price.");
        builder.AppendLine();

        foreach (var candidate in candidates)
        {
            builder.AppendLine($"- {candidate.PublishedAt:O} | {candidate.SourceName} | {candidate.Title} | {candidate.Summary} | {candidate.Url}");
        }

        return builder.ToString();
    }

    private string ResolveModelName()
        => DefaultModelName;

    private static bool ContainsAny(string text, params string[] tokens)
        => tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static decimal NormalizeMarketPrice(decimal price)
        => Math.Clamp(price, 0.01m, 0.99m);

    private static decimal InferMarketPrice(NewsCategory category)
        => category switch
        {
            NewsCategory.Geopolitics => 0.55m,
            NewsCategory.Gold => 0.52m,
            NewsCategory.Crypto => 0.50m,
            NewsCategory.Viral => 0.45m,
            NewsCategory.Market => 0.50m,
            _ => 0.50m
        };

    private static string ResolveDivergenceSignal(decimal aiProbability, decimal marketPrice)
    {
        var gap = aiProbability - marketPrice;

        if (gap >= 0.15m)
        {
            return "LONG";
        }

        if (gap <= -0.15m)
        {
            return "SHORT";
        }

        if (gap >= 0.05m)
        {
            return "WATCH_LONG";
        }

        if (gap <= -0.05m)
        {
            return "WATCH_SHORT";
        }

        return "HOLD";
    }

    private static DeepSeekAnalysisResult Map(DeepSeekAnalysisEntity entity)
    {
        var marketPrice = NormalizeMarketPrice(entity.Confidence);
        var gap = Math.Round(entity.Confidence - marketPrice, 4, MidpointRounding.AwayFromZero);
        var signal = ResolveDivergenceSignal(entity.Confidence, marketPrice);

        return new DeepSeekAnalysisResult(
            entity.Id,
            entity.Category,
            entity.Symbol,
            entity.ModelName,
            entity.Summary,
            entity.Confidence,
            entity.Verdict,
            entity.Reason,
            entity.Confidence,
            marketPrice,
            gap,
            signal,
            entity.KeyPoints,
            entity.RiskFactors,
            entity.SourceUrls,
            entity.GeneratedAt);
    }

    private sealed record DeepSeekAnalysisPayload(
        string Summary,
        decimal Confidence,
        string Verdict,
        string Reason,
        IReadOnlyCollection<string> KeyPoints,
        IReadOnlyCollection<string> RiskFactors,
        string? RawResponse);
}
