using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewsCollector.Api.Controllers;
using NewsCollector.Api.Models;
using NewsCollector.Api.Services;
using NewsCollector.Api.Services.DeepSeekAnalysisService;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Backtesting;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Explainability;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Persistence;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RiskManagement;
using NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;
using NewsCollector.Api.Services.NewsSignalService;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Reporting;

namespace NewsCollector.Api.Tests;

public class DeepSeekAnalysisTests
{
    [Fact]
    public async Task MaturityEndpoint_returns_audit_shape_and_persists_maturity()
    {
        var request = new DeepSeekAnalysisRequest(NewsCategory.Crypto, "BTC", 5, 0.61m);
        var analysisService = new FakeDeepSeekAnalysisService();
        var persistenceService = new FakeDeepSeekAnalysisPersistenceService();
        var controller = BuildController(analysisService, persistenceService, new FakeDeepSeekAnalysisReportingService());

        var response = await controller.AnalyzeDeepSeekMaturity(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<DeepSeekAnalysisAuditPayload>(ok.Value);
        Assert.NotNull(payload.Maturity);
        Assert.NotEmpty(payload.FutureOutcomes);
        Assert.True(persistenceService.StoreMaturityCalled);
        Assert.NotNull(persistenceService.StoredMaturity);
        Assert.NotNull(persistenceService.StoredFutureOutcomes);
    }

    [Fact]
    public async Task ReportEndpoint_returns_summary_from_reporting_service()
    {
        var controller = BuildController(new FakeDeepSeekAnalysisService(), new FakeDeepSeekAnalysisPersistenceService(), new FakeDeepSeekAnalysisReportingService());

        var response = await controller.GetDeepSeekPerformanceReport(NewsCategory.Crypto, "BTC", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var report = Assert.IsType<DeepSeekAnalysisPerformanceReport>(ok.Value);
        Assert.Equal(4, report.TotalAnalyses);
        Assert.Equal(2, report.AnalysesWithMaturity);
        Assert.Equal(1, report.RegimeCounts["RiskOn"]);
    }

    [Fact]
    public void BacktestingService_returns_future_outcomes_for_all_windows()
    {
        var service = new DeepSeekBacktestingService();
        var result = new DeepSeekAnalysisResult(
            Guid.NewGuid(),
            NewsCategory.Market,
            "SPY",
            "deepseek-reasoner",
            "summary",
            0.74m,
            "bullish",
            "reason",
            0.74m,
            0.55m,
            0.19m,
            "LONG",
            ["k1"],
            ["r1"],
            ["u1"],
            DateTimeOffset.UtcNow);

        var outcomes = service.BuildFutureOutcomes(result, "RiskOn");

        Assert.Equal(3, outcomes.Count);
        Assert.Contains(outcomes, x => x.Window == "24h");
        Assert.Contains(outcomes, x => x.Window == "7d");
        Assert.Contains(outcomes, x => x.Window == "30d");
    }

    [Fact]
    public void RiskService_emits_watch_only_or_no_trade_when_edge_is_weak()
    {
        var service = new DeepSeekRiskManagementService();
        var regime = new DeepSeekRegimeAwareness("Choppy", 0.42m, ["Use conservative sizing"]);
        var result = new DeepSeekAnalysisResult(
            Guid.NewGuid(),
            NewsCategory.Crypto,
            "ETH",
            "deepseek-reasoner",
            "summary",
            0.51m,
            "neutral",
            "reason",
            0.51m,
            0.50m,
            0.01m,
            "HOLD",
            ["k1"],
            ["r1"],
            ["u1"],
            DateTimeOffset.UtcNow);

        var risk = service.BuildRiskManagement(result, regime);

        Assert.Contains(risk.Constraints, x => x.Contains("tighter entry criteria", StringComparison.OrdinalIgnoreCase));
    }

    private static NewsController BuildController(
        IDeepSeekAnalysisService deepSeekAnalysisService,
        IDeepSeekAnalysisPersistenceService persistenceService,
        IDeepSeekAnalysisReportingService reportingService)
    {
        var catalog = new InMemoryNewsCatalog(Options.Create(new PolymarketOptions { BaseUrl = "https://gamma-api.polymarket.com" }));
        var signalService = new NewsSignalService();
        var maturityService = new DeepSeekAnalysisMaturityService(
            new DeepSeekRegimeAwarenessService(),
            new DeepSeekSourceWeightingService(),
            new DeepSeekBacktestingService(),
            new DeepSeekRiskManagementService(),
            new DeepSeekExplainabilityService());
        var backtestingService = new DeepSeekBacktestingService();

        return new NewsController(
            catalog,
            signalService,
            deepSeekAnalysisService,
            maturityService,
            backtestingService,
            persistenceService,
            reportingService);
    }

    private sealed class FakeDeepSeekAnalysisService : IDeepSeekAnalysisService
    {
        public Task<DeepSeekAnalysisResult> AnalyzeAsync(DeepSeekAnalysisRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeepSeekAnalysisResult(
                Guid.NewGuid(),
                request.Category,
                request.Symbol,
                "deepseek-reasoner",
                "seed",
                0.63m,
                "bullish",
                "seed",
                0.63m,
                request.MarketPrice ?? 0.55m,
                0.08m,
                "LONG",
                ["k1"],
                ["r1"],
                ["u1"],
                DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<DeepSeekAnalysisResult>> GetLatestAsync(NewsCategory category, string symbol, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeepSeekAnalysisResult>>(Array.Empty<DeepSeekAnalysisResult>());
    }

    private sealed class FakeDeepSeekAnalysisPersistenceService : IDeepSeekAnalysisPersistenceService
    {
        public bool StoreMaturityCalled { get; private set; }
        public DeepSeekAnalysisMaturityLayer? StoredMaturity { get; private set; }
        public IReadOnlyList<DeepSeekAnalysisOutcome>? StoredFutureOutcomes { get; private set; }

        public Task PersistAsync(
            DeepSeekAnalysisResult result,
            IReadOnlyList<string> keyPoints,
            IReadOnlyList<string> riskFactors,
            IReadOnlyList<string> sourceUrls,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StoreMaturityAsync(
            DeepSeekAnalysisResult result,
            DeepSeekAnalysisMaturityLayer maturity,
            IReadOnlyList<DeepSeekAnalysisOutcome> futureOutcomes,
            CancellationToken cancellationToken)
        {
            StoreMaturityCalled = true;
            StoredMaturity = maturity;
            StoredFutureOutcomes = futureOutcomes;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeepSeekAnalysisReportingService : IDeepSeekAnalysisReportingService
    {
        public Task<DeepSeekAnalysisPerformanceReport> BuildAsync(NewsCategory? category, string? symbol, CancellationToken cancellationToken)
            => Task.FromResult(new DeepSeekAnalysisPerformanceReport(
                4,
                2,
                0.71m,
                0.09m,
                0.62m,
                15.2m,
                22.4m,
                new Dictionary<string, int> { ["RiskOn"] = 1, ["Neutral"] = 1 },
                DateTimeOffset.UtcNow));
    }
}
