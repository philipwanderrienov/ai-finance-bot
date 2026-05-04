using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Reporting;

public interface IDeepSeekAnalysisReportingService
{
    Task<DeepSeekAnalysisPerformanceReport> BuildAsync(NewsCategory? category, string? symbol, CancellationToken cancellationToken);
}

public sealed class DeepSeekAnalysisReportingService : IDeepSeekAnalysisReportingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly NewsDbContext _dbContext;

    public DeepSeekAnalysisReportingService(NewsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeepSeekAnalysisPerformanceReport> BuildAsync(NewsCategory? category, string? symbol, CancellationToken cancellationToken)
    {
        var query = _dbContext.DeepSeekAnalyses.AsNoTracking();

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            query = query.Where(x => x.Symbol == symbol);
        }

        var rows = await query
            .OrderByDescending(x => x.GeneratedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var analyses = rows.Count;
        var withMaturity = 0;
        var totalConfidence = 0m;
        var totalGap = 0m;
        var totalBacktests = 0;
        var totalBacktestAccuracy = 0m;
        var totalReturnBps = 0m;
        var maxDrawdownBps = 0m;
        var regimeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            totalConfidence += row.Confidence;
            totalGap += row.Confidence - NormalizeMarketPrice(row.Confidence);

            var maturity = TryParseMaturity(row.MaturityPayloadJson);
            if (maturity is not null)
            {
                withMaturity++;
                Increment(regimeCounts, maturity.RegimeAwareness?.Regime);

                foreach (var backtest in maturity.Backtests)
                {
                    totalBacktests++;
                    totalBacktestAccuracy += backtest.DirectionalAccuracy;
                    totalReturnBps += backtest.AvgReturnBps;
                    maxDrawdownBps = Math.Max(maxDrawdownBps, backtest.MaxDrawdownBps);
                }
            }
        }

        var averageConfidence = analyses == 0 ? 0m : Math.Round(totalConfidence / analyses, 4, MidpointRounding.AwayFromZero);
        var averageGap = analyses == 0 ? 0m : Math.Round(totalGap / analyses, 4, MidpointRounding.AwayFromZero);
        var averageBacktestAccuracy = totalBacktests == 0 ? 0m : Math.Round(totalBacktestAccuracy / totalBacktests, 4, MidpointRounding.AwayFromZero);
        var averageReturnBps = totalBacktests == 0 ? 0m : Math.Round(totalReturnBps / totalBacktests, 2, MidpointRounding.AwayFromZero);

        return new DeepSeekAnalysisPerformanceReport(
            TotalAnalyses: analyses,
            AnalysesWithMaturity: withMaturity,
            AverageConfidence: averageConfidence,
            AverageGap: averageGap,
            AverageBacktestAccuracy: averageBacktestAccuracy,
            AverageReturnBps: averageReturnBps,
            MaxDrawdownBps: Math.Round(maxDrawdownBps, 2, MidpointRounding.AwayFromZero),
            RegimeCounts: regimeCounts.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private static DeepSeekAnalysisMaturityLayer? TryParseMaturity(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DeepSeekAnalysisMaturityLayer>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void Increment(IDictionary<string, int> counts, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "Unknown";
        }

        counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;
    }

    private static decimal NormalizeMarketPrice(decimal price)
        => Math.Clamp(price, 0.01m, 0.99m);
}

public sealed record DeepSeekAnalysisPerformanceReport(
    int TotalAnalyses,
    int AnalysesWithMaturity,
    decimal AverageConfidence,
    decimal AverageGap,
    decimal AverageBacktestAccuracy,
    decimal AverageReturnBps,
    decimal MaxDrawdownBps,
    IReadOnlyDictionary<string, int> RegimeCounts,
    DateTimeOffset GeneratedAtUtc);
