using Microsoft.AspNetCore.Mvc;
using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService;
using NewsCollector.Api.Services.NewsSignalService;
using NewsCollector.Api.Services;

namespace NewsCollector.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController : ControllerBase
{
    private readonly INewsCatalog _catalog;
    private readonly INewsSignalService _signalService;
    private readonly IDeepSeekAnalysisService _deepSeekAnalysisService;
    private readonly IDeepSeekAnalysisMaturityService _deepSeekAnalysisMaturityService;

    public NewsController(
        INewsCatalog catalog,
        INewsSignalService signalService,
        IDeepSeekAnalysisService deepSeekAnalysisService,
        IDeepSeekAnalysisMaturityService deepSeekAnalysisMaturityService)
    {
        _catalog = catalog;
        _signalService = signalService;
        _deepSeekAnalysisService = deepSeekAnalysisService;
        _deepSeekAnalysisMaturityService = deepSeekAnalysisMaturityService;
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
    public async Task<ActionResult<DeepSeekAnalysisMaturityLayer>> AnalyzeDeepSeekMaturity(
        [FromBody] DeepSeekAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var analysis = await _deepSeekAnalysisService.AnalyzeAsync(request, cancellationToken);
        var maturity = _deepSeekAnalysisMaturityService.BuildMaturityLayer(analysis, simulatorMode: string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")));
        return Ok(maturity);
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
