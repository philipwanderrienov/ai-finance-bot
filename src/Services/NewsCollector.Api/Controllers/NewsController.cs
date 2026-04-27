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

    public NewsController(INewsCatalog catalog, INewsSignalService signalService, IDeepSeekAnalysisService deepSeekAnalysisService)
    {
        _catalog = catalog;
        _signalService = signalService;
        _deepSeekAnalysisService = deepSeekAnalysisService;
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
