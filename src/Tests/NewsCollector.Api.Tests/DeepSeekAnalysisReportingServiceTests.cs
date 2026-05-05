using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NewsCollector.Api.Data;
using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Reporting;

namespace NewsCollector.Api.Tests;

public class DeepSeekAnalysisReportingServiceTests
{
    [Fact]
    public async Task BuildAsync_returns_empty_report_when_no_rows_exist()
    {
        await using var dbContext = CreateDbContext();
        var service = new DeepSeekAnalysisReportingService(dbContext);

        var report = await service.BuildAsync(null, null, CancellationToken.None);

        Assert.Equal(0, report.TotalAnalyses);
        Assert.Equal(0, report.AnalysesWithMaturity);
        Assert.Equal(0m, report.AverageConfidence);
        Assert.Equal(0m, report.AverageGap);
        Assert.Equal(0m, report.AverageBacktestAccuracy);
        Assert.Equal(0m, report.AverageReturnBps);
        Assert.Equal(0m, report.MaxDrawdownBps);
        Assert.Empty(report.RegimeCounts);
    }

    [Fact]
    public async Task BuildAsync_aggregates_filtered_rows_and_parses_maturity_payload()
    {
        await using var dbContext = CreateDbContext();
        dbContext.DeepSeekAnalyses.AddRange(
            new DeepSeekAnalysisEntity
            {
                Id = Guid.NewGuid(),
                Category = NewsCategory.Crypto,
                Symbol = "BTC",
                ModelName = "model-a",
                Summary = "summary-a",
                Confidence = 0.80m,
                Verdict = "bullish",
                Reason = "reason-a",
                GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                InputFingerprint = "fp-a",
                MaturityPayloadJson = """
                {
                  "metadata": {
                    "analysisVersion": "v1",
                    "marketRegime": "RiskOn",
                    "generatedAtUtc": "2026-05-05T00:00:00+00:00",
                    "backtestWindows": ["24h"],
                    "sourceWeights": ["news"],
                    "simulatorMode": true,
                    "modelName": "model-a"
                  },
                  "backtests": [
                    {
                      "window": "24h",
                      "directionalAccuracy": 0.75,
                      "avgReturnBps": 12.5,
                      "maxDrawdownBps": 4.2,
                      "sampleCount": 10
                    }
                  ],
                  "riskManagement": {
                    "action": "HOLD",
                    "positionSizeMultiplier": 0.5,
                    "stopLossPct": 0.03,
                    "takeProfitPct": 0.08,
                    "confidenceFloor": 0.60,
                    "constraints": ["tight"]
                  },
                  "explainability": [],
                  "sourceWeights": [],
                  "regimeAwareness": {
                    "regime": "RiskOn",
                    "confidence": 0.81,
                    "implications": ["tailwind"]
                  }
                }
                """
            },
            new DeepSeekAnalysisEntity
            {
                Id = Guid.NewGuid(),
                Category = NewsCategory.Crypto,
                Symbol = "BTC",
                ModelName = "model-b",
                Summary = "summary-b",
                Confidence = 0.60m,
                Verdict = "neutral",
                Reason = "reason-b",
                GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                InputFingerprint = "fp-b"
            },
            new DeepSeekAnalysisEntity
            {
                Id = Guid.NewGuid(),
                Category = NewsCategory.Market,
                Symbol = "SPY",
                ModelName = "model-c",
                Summary = "summary-c",
                Confidence = 0.95m,
                Verdict = "bullish",
                Reason = "reason-c",
                GeneratedAt = DateTimeOffset.UtcNow,
                InputFingerprint = "fp-c"
            });

        await dbContext.SaveChangesAsync();

        var service = new DeepSeekAnalysisReportingService(dbContext);
        var report = await service.BuildAsync(NewsCategory.Crypto, "BTC", CancellationToken.None);

        Assert.Equal(2, report.TotalAnalyses);
        Assert.Equal(1, report.AnalysesWithMaturity);
        Assert.Equal(0.70m, report.AverageConfidence);
        Assert.Equal(0m, report.AverageGap);
        Assert.Equal(0.75m, report.AverageBacktestAccuracy);
        Assert.Equal(12.5m, report.AverageReturnBps);
        Assert.Equal(4.2m, report.MaxDrawdownBps);
        Assert.Equal(1, report.RegimeCounts["RiskOn"]);
    }

    private static NewsDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ReportingTestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class ReportingTestDbContext : NewsDbContext
    {
        public ReportingTestDbContext(DbContextOptions<NewsDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DeepSeekAnalysisEntity>(entity =>
            {
                entity.Property(x => x.KeyPoints)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value),
                        value => JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>());

                entity.Property(x => x.RiskFactors)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value),
                        value => JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>());

                entity.Property(x => x.SourceUrls)
                    .HasColumnType("TEXT")
                    .HasConversion(
                        value => JsonSerializer.Serialize(value),
                        value => JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>());
            });
        }
    }
}
