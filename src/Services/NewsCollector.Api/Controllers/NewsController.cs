using Microsoft.AspNetCore.Mvc;
using NewsCollector.Api.Models;
using NewsCollector.Api.Services;
using NewsCollector.Api.Services.DeepSeekAnalysisService;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Backtesting;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Persistence;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Reporting;
using NewsCollector.Api.Services.NewsSignalService;

namespace NewsCollector.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController : ControllerBase
{
    private readonly INewsCatalog _catalog;
    private readonly INewsSignalService _signalService;
    private readonly IDeepSeekAnalysisService _deepSeekAnalysisService;
    private readonly IDeepSeekAnalysisMaturityService _deepSeekAnalysisMaturityService;
    private readonly IDeepSeekBacktestingService _deepSeekBacktestingService;
    private readonly IDeepSeekAnalysisPersistenceService _persistenceService;
    private readonly IDeepSeekAnalysisReportingService _reportingService;

    public NewsController(
        INewsCatalog catalog,
        INewsSignalService signalService,
        IDeepSeekAnalysisService deepSeekAnalysisService,
        IDeepSeekAnalysisMaturityService deepSeekAnalysisMaturityService,
        IDeepSeekBacktestingService deepSeekBacktestingService,
        IDeepSeekAnalysisPersistenceService persistenceService,
        IDeepSeekAnalysisReportingService reportingService)
    {
        _catalog = catalog;
        _signalService = signalService;
        _deepSeekAnalysisService = deepSeekAnalysisService;
        _deepSeekAnalysisMaturityService = deepSeekAnalysisMaturityService;
        _deepSeekBacktestingService = deepSeekBacktestingService;
        _persistenceService = persistenceService;
        _reportingService = reportingService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<NewsItem>> GetLatestNews()
        => Ok(_catalog.GetLatest());

    [HttpGet("signals")]
    public ActionResult<IReadOnlyCollection<NewsSignal>> GetSignals()
        => Ok(_signalService.Analyze(_catalog.GetLatest()).ToArray());

    [HttpPost("deepseek/analyze")]
    public async Task<ActionResult<DeepSeekAnalysisResult>> AnalyzeDeepSeek([FromBody] DeepSeekAnalysisRequest request, CancellationToken cancellationToken)
    {
        var result = await _deepSeekAnalysisService.AnalyzeAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("deepseek/analyze/maturity")]
    public async Task<ActionResult<DeepSeekAnalysisAuditPayload>> AnalyzeDeepSeekMaturity(
        [FromBody] DeepSeekAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var analysis = await _deepSeekAnalysisService.AnalyzeAsync(request, cancellationToken);
        var simulatorMode = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
        var maturity = _deepSeekAnalysisMaturityService.BuildMaturityLayer(analysis, simulatorMode);
        var futureOutcomes = _deepSeekBacktestingService.BuildFutureOutcomes(analysis, maturity.RegimeAwareness.Regime);
        await _persistenceService.StoreMaturityAsync(analysis, maturity, futureOutcomes, cancellationToken);

        return Ok(new DeepSeekAnalysisAuditPayload(analysis, maturity, futureOutcomes, null));
    }

    [HttpGet("deepseek/report")]
    public async Task<ActionResult<DeepSeekAnalysisPerformanceReport>> GetDeepSeekPerformanceReport(
        [FromQuery] NewsCategory? category = null,
        [FromQuery] string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _reportingService.BuildAsync(category, symbol, cancellationToken);
        return Ok(report);
    }

    [HttpGet("deepseek/analyze")]
    public async Task<ActionResult<IReadOnlyCollection<DeepSeekAnalysisResult>>> GetDeepSeekAnalyses(
        [FromQuery] NewsCategory category,
        [FromQuery] string symbol,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await _deepSeekAnalysisService.GetLatestAsync(category, symbol, take, cancellationToken);
        return Ok(results);
    }
}
